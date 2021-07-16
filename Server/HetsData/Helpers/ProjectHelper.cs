﻿using System;
using System.Collections.Generic;
using System.Linq;
using HetsData.Dtos;
using HetsData.Model;
using Microsoft.EntityFrameworkCore;

namespace HetsData.Helpers
{
    #region Project Models

    public class ProjectLite
    {
        public int Id { get; set; }
        public DistrictDto District { get; set; }
        public string Name { get; set; }
        public ContactDto PrimaryContact { get; set; }        
        public int? Hires { get; set; }
        public int? Requests { get; set; }
        public string Status { get; set; }
        public string ProvincialProjectNumber { get; set; }
        public string FiscalYear { get; set; }
    }

    public class ProjectLiteList
    {
        public int Id { get; set; }
        public string Name { get; set; }        
    }

    public class ProjectAgreementSummary
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public List<int> AgreementIds { get; set; }
    }

    public class ProjectRentalAgreementClone
    {
        public int ProjectId { get; set; }
        public int AgreementToCloneId { get; set; }
        public int RentalAgreementId { get; set; }
    }

    #endregion

    public static class ProjectHelper
    {
        #region Convert full project record to a "Lite" version



        #endregion

        #region Get Project History

        public static List<History> GetHistoryRecords(int id, int? offset, int? limit, DbAppContext context)
        {
            HetProject project = context.HetProject.AsNoTracking()
                .Include(x => x.HetHistory)
                .First(a => a.ProjectId == id);

            List<HetHistory> data = project.HetHistory
                .OrderByDescending(y => y.AppLastUpdateTimestamp)
                .ToList();

            if (offset == null)
            {
                offset = 0;
            }

            if (limit == null)
            {
                limit = data.Count - offset;
            }

            List<History> result = new List<History>();

            for (int i = (int)offset; i < data.Count && i < offset + limit; i++)
            {
                History temp = new History();

                if (data[i] != null)
                {
                    temp.HistoryText = data[i].HistoryText;
                    temp.Id = data[i].HistoryId;
                    temp.LastUpdateTimestamp = data[i].AppLastUpdateTimestamp;
                    temp.LastUpdateUserid = data[i].AppLastUpdateUserid;
                    temp.AffectedEntityId = data[i].ProjectId;
                }

                result.Add(temp);
            }

            return result;
        }

        #endregion
    }
}
