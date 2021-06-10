using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Swashbuckle.AspNetCore.Annotations;
using HetsApi.Authorization;
using HetsApi.Helpers;
using HetsApi.Model;
using HetsData.View;
using HetsData.Model;
using HetsData.Hangfire;
using Hangfire.Storage;
using Newtonsoft.Json;
using Hangfire.Common;

namespace HetsApi.Controllers
{
    /// <summary>
    /// District Controller
    /// </summary>
    [Route("api/districts")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public class DistrictController : Controller
    {
        private readonly Object _thisLock = new Object();
        private readonly DbAppContext _context;
        private readonly IConfiguration _configuration;
        private readonly IAnnualRollover _annualRollover;
        private IMonitoringApi _monitoringApi;

        public DistrictController(DbAppContext context, IConfiguration configuration, IHttpContextAccessor httpContextAccessor, IAnnualRollover annualRollover)
        {
            _context = context;
            _configuration = configuration;
            _annualRollover = annualRollover;
            _monitoringApi = JobStorage.Current.GetMonitoringApi();
        }

        /// <summary>
        /// Get all districts
        /// </summary>
        [HttpGet]
        [Route("")]
        [SwaggerOperation("DistrictsGet")]
        [SwaggerResponse(200, type: typeof(List<HetDistrict>))]
        [AllowAnonymous]
        public virtual IActionResult DistrictsGet()
        {
            List<HetDistrict> districts = _context.HetDistrict.AsNoTracking()
                .Include(x => x.Region)
                .ToList();

            return new ObjectResult(new HetsResponse(districts));
        }

        #region Owners by District

        /// <summary>
        /// Get all owners by district
        /// </summary>
        [HttpGet]
        [Route("{id}/owners")]
        [SwaggerOperation("DistrictOwnersGet")]
        [SwaggerResponse(200, type: typeof(List<HetOwner>))]
        [RequiresPermission(HetPermission.Login)]
        public virtual IActionResult DistrictOwnersGet([FromRoute]int id)
        {
            bool exists = _context.HetDistrict.Any(a => a.DistrictId == id);

            // not found
            if (!exists) return new NotFoundObjectResult(new HetsResponse("HETS-01", ErrorViewModel.GetDescription("HETS-01", _configuration)));

            List<HetOwner> owners = _context.HetOwner.AsNoTracking()
                .Where(x => x.LocalArea.ServiceArea.District.DistrictId == id)
                .OrderBy(x => x.OrganizationName)
                .ToList();

            return new ObjectResult(new HetsResponse(owners));
        }

        #endregion

        #region Local Areas by District

        /// <summary>
        /// Get all local areas by district
        /// </summary>
        [HttpGet]
        [Route("{id}/localAreas")]
        [SwaggerOperation("DistrictLocalAreasGet")]
        [SwaggerResponse(200, type: typeof(List<HetLocalArea>))]
        [AllowAnonymous]
        public virtual IActionResult DistrictLocalAreasGet([FromRoute]int id)
        {
            bool exists = _context.HetDistrict.Any(a => a.DistrictId == id);

            // not found
            if (!exists) return new ObjectResult(new HetsResponse(new List<HetLocalArea>()));

            List<HetLocalArea> localAreas = _context.HetLocalArea.AsNoTracking()
                .Where(x => x.ServiceArea.District.DistrictId == id)
                .OrderBy(x => x.Name)
                .ToList();

            return new ObjectResult(new HetsResponse(localAreas));
        }

        #endregion

        #region District Rollover

        /// <summary>
        /// Get district rollover status
        /// </summary>
        [HttpGet]
        [Route("{id}/rolloverStatus")]
        [SwaggerOperation("RolloverStatusGet")]
        [SwaggerResponse(200, type: typeof(HetDistrictStatus))]
        [RequiresPermission(HetPermission.Login)]
        public virtual IActionResult RolloverStatusGet([FromRoute]int id)
        {
            var typeFullName = "HetsData.Hangfire.AnnualRollover";
            var methodName = "AnnualRolloverJob";
            var rolloverJob = $"{typeFullName}-{methodName}-{id}";

            var jobProcessing = _monitoringApi.ProcessingJobs(0, 10000)
                .ToList();

            var jobExists = jobProcessing.Any(x => GetJobFingerprint(x.Value.Job) == rolloverJob);

            var status = _annualRollover.GetRecord(id);

            var progress = _context.HetRolloverProgress.FirstOrDefault(a => a.DistrictId == id);

            // not found
            if (progress == null) return new ObjectResult(new HetsResponse(new RolloverProgressDto { DistrictId = id, ProgressPercentage = null }));

            if (!jobExists)
            {
                return new ObjectResult(new HetsResponse(new RolloverProgressDto { DistrictId = id, ProgressPercentage = status.ProgressPercentage }));
            }

            // get status of current district
            return new ObjectResult(new HetsResponse(new RolloverProgressDto { DistrictId = id, ProgressPercentage = progress.ProgressPercentage }));
        }

        private string GetJobFingerprint(Job job)
        {
            var args = "";

            if (job.Args.Count > 0)
            {
                args = job.Args[0].ToString();
            }

            return $"{job.Type.FullName}-{job.Method.Name}-{args}";
        }


        /// <summary>
        /// Dismiss district rollover status message
        /// </summary>
        [HttpPost]
        [Route("{id}/dismissRolloverMessage")]
        [SwaggerOperation("DismissRolloverMessagePost")]
        [SwaggerResponse(200, type: typeof(HetDistrictStatus))]
        [RequiresPermission(HetPermission.Login, HetPermission.WriteAccess)]
        public virtual IActionResult DismissRolloverMessagePost([FromRoute]int id)
        {
            bool exists = _context.HetDistrictStatus.Any(a => a.DistrictId == id);

            // not found - return new status record
            if (!exists) return NotFound();

            // get record and update
            HetDistrictStatus status = _context.HetDistrictStatus
                .First(a => a.DistrictId == id);

            // ensure the process is complete
            if (status.DisplayRolloverMessage != null &&
                status.DisplayRolloverMessage == true &&
                status.ProgressPercentage != null &&
                status.ProgressPercentage == 100)
            {
                status.ProgressPercentage = null;
                status.DisplayRolloverMessage = false;
            }

            var progress = _context.HetRolloverProgress.FirstOrDefault(a => a.DistrictId == id);

            progress.ProgressPercentage = null;

            _context.SaveChanges();

            // get status of current district
            return new ObjectResult(new HetsResponse(_annualRollover.GetRecord(id)));
        }

        /// <summary>
        /// Start the annual rollover process
        /// </summary>
        [HttpGet]
        [Route("{id}/annualRollover")]
        [SwaggerOperation("AnnualRolloverGet")]
        [RequiresPermission(HetPermission.DistrictRollover)]
        public virtual IActionResult AnnualRolloverGet([FromRoute]int id)
        {
            bool exists = _context.HetDistrict.Any(a => a.DistrictId == id);

            // not found
            if (!exists) return new NotFoundObjectResult(new HetsResponse("HETS-01", ErrorViewModel.GetDescription("HETS-01", _configuration)));

            // determine the current fiscal year
            DateTime fiscalStart;

            if (DateTime.UtcNow.Month == 1 || DateTime.UtcNow.Month == 2 || DateTime.UtcNow.Month == 3)
            {
                fiscalStart = new DateTime(DateTime.UtcNow.AddYears(-1).Year, 4, 1);
            }
            else
            {
                fiscalStart = new DateTime(DateTime.UtcNow.Year, 4, 1);
            }

            // get record and ensure it isn't already processing
            HetDistrictStatus status = _annualRollover.GetRecord(id);

            if (status == null)
            {
                return new NotFoundObjectResult(new HetsResponse("HETS-01", ErrorViewModel.GetDescription("HETS-01", _configuration)));
            }

            if (status.CurrentFiscalYear == fiscalStart.Year)
            {
                // return - cannot rollover again
                return new ObjectResult(status);
            }

            if (status.DisplayRolloverMessage == true ||
                (status.ProgressPercentage != null && status.ProgressPercentage > 0))
            {
                // return already active
                return new ObjectResult(status);
            }

            // serialize scoring rules from config into json string
            IConfigurationSection scoringRules = _configuration.GetSection("SeniorityScoringRules");
            string seniorityScoringRules = GetConfigJson(scoringRules);

            // queue the job
            BackgroundJob.Enqueue<AnnualRollover>(x => x.AnnualRolloverJob(id, seniorityScoringRules));
            var progressDto = _annualRollover.KickoffProgress(id);

            return new ObjectResult(progressDto);
        }

        #endregion

        #region Get Scoring Rules

        private string GetConfigJson(IConfigurationSection scoringRules)
        {
            string jsonString = RecurseConfigJson(scoringRules);

            if (jsonString.EndsWith("},"))
            {
                jsonString = jsonString.Substring(0, jsonString.Length - 1);
            }

            return jsonString;
        }

        private string RecurseConfigJson(IConfigurationSection scoringRules)
        {
            StringBuilder temp = new StringBuilder();

            temp.Append("{");

            // check for children
            foreach (IConfigurationSection section in scoringRules.GetChildren())
            {
                temp.Append(@"""" + section.Key + @"""" + ":");

                if (section.Value == null)
                {
                    temp.Append(RecurseConfigJson(section));
                }
                else
                {
                    temp.Append(@"""" + section.Value + @"""" + ",");
                }
            }

            string jsonString = temp.ToString();

            if (jsonString.EndsWith(","))
            {
                jsonString = jsonString.Substring(0, jsonString.Length - 1);
            }

            jsonString = jsonString + "},";
            return jsonString;
        }

        #endregion

        #region Fiscal Years by District

        /// <summary>
        /// Get all fiscal years by district
        /// </summary>
        [HttpGet]
        [Route("{id}/fiscalYears")]
        [SwaggerOperation("DistrictFiscalYearsGet")]
        [SwaggerResponse(200, type: typeof(List<HetOwner>))]
        [RequiresPermission(HetPermission.Login)]
        public virtual IActionResult DistrictFiscalYearsGet([FromRoute]int id)
        {
            bool exists = _context.HetDistrict.Any(a => a.DistrictId == id);

            // not found
            if (!exists) return new NotFoundObjectResult(new HetsResponse("HETS-01", ErrorViewModel.GetDescription("HETS-01", _configuration)));

             HetDistrictStatus status = _context.HetDistrictStatus
                .AsNoTracking()
                .FirstOrDefault(x => x.DistrictId == id);

            if (status == null) return new NotFoundObjectResult(new HetsResponse("HETS-01", ErrorViewModel.GetDescription("HETS-01", _configuration)));

            List<string> fiscalYears = new List<string>();

            string current = $"{status.CurrentFiscalYear.ToString()}/{(status.CurrentFiscalYear + 1).ToString()}";
            string next = $"{status.NextFiscalYear.ToString()}/{(status.NextFiscalYear + 1).ToString()}";

            fiscalYears.Add(current);
            fiscalYears.Add(next);

            return new ObjectResult(new HetsResponse(fiscalYears));
        }

        #endregion
    }
}
