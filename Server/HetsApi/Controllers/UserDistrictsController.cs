using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using HetsApi.Authorization;
using HetsApi.Helpers;
using HetsApi.Model;
using HetsData.Entities;
using Microsoft.AspNetCore.Authorization;
using AutoMapper;
using HetsData.Dtos;

namespace HetsApi.Controllers
{
    /// <summary>
    /// User District Controller
    /// </summary>
    [Route("api/userDistricts")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public class UserDistrictController : ControllerBase
    {
        private readonly DbAppContext _context;
        private readonly IConfiguration _configuration;
        private readonly HttpContext _httpContext;
        private readonly IMapper _mapper;

        public UserDistrictController(DbAppContext context, IConfiguration configuration, 
            IHttpContextAccessor httpContextAccessor, IMapper mapper)
        {
            _context = context;
            _configuration = configuration;
            _httpContext = httpContextAccessor.HttpContext;
            _mapper = mapper;
        }

        /// <summary>
        /// Get all districts for the logged on user
        /// </summary>
        [HttpGet]
        [Route("")]
        [AllowAnonymous]
        public virtual ActionResult<List<UserDistrictDto>> UserDistrictsGet()
        {
            // return for the current user only
            string userId = _context.SmUserId;

            List<HetUserDistrict> result = _context.HetUserDistricts.AsNoTracking()
                .Include(x => x.User)
                .Include(x => x.District)
                .Where(x => x.User.SmUserId.ToUpper() == userId)
                .ToList();

            return new ObjectResult(new HetsResponse(_mapper.Map<List<UserDistrictDto>>(result)));
        }

        /// <summary>
        /// Delete user district
        /// </summary>
        /// <param name="id">id of User District to delete</param>
        /// <response code="200">OK</response>
        [HttpPost]
        [Route("{id}/delete")]
        [RequiresPermission(HetPermission.UserManagement, HetPermission.WriteAccess)]
        public virtual ActionResult<List<UserDistrictDto>> UserDistrictsIdDeletePost([FromRoute]int id)
        {
            bool exists = _context.HetUserDistricts.Any(a => a.UserDistrictId == id);

            // not found
            if (!exists) return new NotFoundObjectResult(new HetsResponse("HETS-01", ErrorViewModel.GetDescription("HETS-01", _configuration)));

            // get record
            HetUserDistrict item = _context.HetUserDistricts
                .Include(x => x.User)
                .First(a => a.UserDistrictId == id);

            int userId = item.User.UserId;

            // remove record
            _context.HetUserDistricts.Remove(item);
            _context.SaveChanges();

            // return the updated user district records
            List<HetUserDistrict> result = _context.HetUserDistricts.AsNoTracking()
                .Include(x => x.User)
                .Include(x => x.District)
                .Where(x => x.User.UserId == userId)
                .ToList();

            return new ObjectResult(new HetsResponse(_mapper.Map<List<UserDistrictDto>>(result)));
        }

        /// <summary>
        /// Create or update a User District
        /// </summary>
        /// <param name="id"></param>
        /// <param name="item"></param>
        [HttpPost]
        [Route("{id}")]
        [RequiresPermission(HetPermission.UserManagement, HetPermission.WriteAccess)]
        public virtual ActionResult<List<UserDistrictDto>> UserDistrictsIdPost([FromRoute]int id, [FromBody]UserDistrictDto item)
        {
            // not found
            if (id != item.UserDistrictId) return new NotFoundObjectResult(new HetsResponse("HETS-01", ErrorViewModel.GetDescription("HETS-01", _configuration)));

            // not found
            if (item.User == null) return new NotFoundObjectResult(new HetsResponse("HETS-01", ErrorViewModel.GetDescription("HETS-01", _configuration)));

            int userId = item.User.UserId;

            // get record
            List<HetUserDistrict> userDistricts = _context.HetUserDistricts
                .Include(x => x.User)
                .Include(x => x.District)
                .Where(x => x.User.UserId == userId)
                .ToList();

            bool districtExists;
            bool hasPrimary = false;

            // add or update user district
            if (item.UserDistrictId > 0)
            {
                int index = userDistricts.FindIndex(a => a.UserDistrictId == item.UserDistrictId);

                // not found
                if (index < 0) return new NotFoundObjectResult(new HetsResponse("HETS-01", ErrorViewModel.GetDescription("HETS-01", _configuration)));

                // check if this district already exists
                districtExists = userDistricts.Exists(a => a.District.DistrictId == item.District.DistrictId);

                // update the record
                if (!districtExists)
                {
                    if (item.User != null)
                    {
                        userDistricts.ElementAt(index).UserId = item.User.UserId;
                    }
                    else
                    {
                        // user required
                        return new BadRequestObjectResult(new HetsResponse("HETS-17",
                            ErrorViewModel.GetDescription("HETS-17", _configuration)));
                    }

                    if (item.District != null)
                    {
                        userDistricts.ElementAt(index).DistrictId = item.District.DistrictId;
                    }
                    else
                    {
                        // district required
                        return new BadRequestObjectResult(new HetsResponse("HETS-18",
                            ErrorViewModel.GetDescription("HETS-18", _configuration)));
                    }
                }

                // manage the primary attribute
                if (item.IsPrimary)
                {
                    userDistricts.ElementAt(index).IsPrimary = true;

                    foreach (HetUserDistrict existingUserDistrict in userDistricts)
                    {
                        if (existingUserDistrict.IsPrimary &&
                            existingUserDistrict.UserDistrictId != item.UserDistrictId)
                        {
                            existingUserDistrict.IsPrimary = false;
                            break;
                        }
                    }
                }
                else
                {
                    userDistricts[index].IsPrimary = false;

                    foreach (HetUserDistrict existingUserDistrict in userDistricts)
                    {
                        if (existingUserDistrict.IsPrimary &&
                            existingUserDistrict.UserDistrictId != item.UserDistrictId)
                        {
                            hasPrimary = true;
                            break;
                        }
                    }

                    if (!hasPrimary)
                    {
                        userDistricts[index].IsPrimary = true;
                    }
                }
            }
            else  // add user district
            {
                // check if this district already exists
                districtExists = userDistricts.Exists(a => a.District.DistrictId == item.District.DistrictId);

                // add the record
                if (!districtExists)
                {
                    if (item.User != null)
                    {
                        var user = _context.HetUsers.FirstOrDefault(a => a.UserId == item.User.UserId);
                    }
                    else
                    {
                        // user required
                        return new BadRequestObjectResult(new HetsResponse("HETS-17", ErrorViewModel.GetDescription("HETS-17", _configuration)));
                    }

                    if (item.District != null)
                    {
                        var district = _context.HetDistricts.FirstOrDefault(a => a.DistrictId == item.District.DistrictId);
                    }
                    else
                    {
                        // district required
                        return new BadRequestObjectResult(new HetsResponse("HETS-18", ErrorViewModel.GetDescription("HETS-18", _configuration)));
                    }

                    if (item.IsPrimary)
                    {
                        item.IsPrimary = true;

                        foreach (HetUserDistrict existingUserDistrict in userDistricts)
                        {
                            if (existingUserDistrict.IsPrimary)
                            {
                                existingUserDistrict.IsPrimary = false;
                                break;
                            }
                        }
                    }
                    else
                    {
                        item.IsPrimary = false;

                        foreach (HetUserDistrict existingUserDistrict in userDistricts)
                        {
                            if (existingUserDistrict.IsPrimary)
                            {
                                hasPrimary = true;
                                break;
                            }
                        }

                        if (!hasPrimary)
                        {
                            item.IsPrimary = true;
                        }
                    }

                    _context.HetUserDistricts.Add(_mapper.Map<HetUserDistrict>(item));
                }
            }

            _context.SaveChanges();

            // return the updated user district records
            List<HetUserDistrict> result = _context.HetUserDistricts.AsNoTracking()
                .Include(x => x.User)
                .Include(x => x.District)
                .Where(x => x.User.UserId == userId)
                .ToList();

            return new ObjectResult(new HetsResponse(_mapper.Map<List<UserDistrictDto>>(result)));
        }

        /// <summary>
        /// Switch User District
        /// </summary>
        /// <param name="id"></param>
        [HttpPost]
        [Route("{id}/switch")]
        [RequiresPermission(HetPermission.Login, HetPermission.WriteAccess)]
        public virtual ActionResult<UserDto> UserDistrictsIdSwitchPost([FromRoute]int id)
        {
            bool exists = _context.HetUserDistricts.Any(a => a.UserDistrictId == id);

            // not found
            if (!exists) return new NotFoundObjectResult(new HetsResponse("HETS-01", ErrorViewModel.GetDescription("HETS-01", _configuration)));

            // get record
            HetUserDistrict userDistrict = _context.HetUserDistricts.First(a => a.UserDistrictId == id);

            string userId = _context.SmUserId;

            HetUser user = _context.HetUsers.First(a => a.SmUserId.ToUpper() == userId);
            user.DistrictId = userDistrict.DistrictId;

            _context.SaveChanges();

            // create new district switch cookie
            _httpContext.Response.Cookies.Append(
                "HETSDistrict",
                userDistrict.DistrictId.ToString(),
                new CookieOptions
                {
                    Path = "/",
                    Secure = true,
                    SameSite = SameSiteMode.None
                }
            );

            return new ObjectResult(new HetsResponse(_mapper.Map<UserDto>(user)));
        }
    }
}
