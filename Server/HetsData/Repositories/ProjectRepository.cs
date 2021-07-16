﻿using AutoMapper;
using HetsData.Dtos;
using HetsData.Helpers;
using HetsData.Model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace HetsData.Repositories
{
    public interface IProjectRepository
    {
        ProjectDto GetRecord(int projectId, int? districtId = 0);
        ProjectLite ToLiteModel(HetProject project);
    }
    public class ProjectRepository : IProjectRepository
    {
        private DbAppContext _dbContext;
        private IMapper _mapper;

        public ProjectRepository(DbAppContext dbContext, IMapper mapper)
        {
            _dbContext = dbContext;
            _mapper = mapper;
        }

        /// <summary>
        /// Get a Project record
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="districtId"></param>
        /// <returns></returns>
        public ProjectDto GetRecord(int projectId, int? districtId = 0)
        {
            HetProject project = _dbContext.HetProject.AsNoTracking()
                .Include(x => x.ProjectStatusType)
                .Include(x => x.District)
                    .ThenInclude(x => x.Region)
                .Include(x => x.HetContact)
                .Include(x => x.PrimaryContact)
                .Include(x => x.HetRentalRequest)
                    .ThenInclude(y => y.DistrictEquipmentType)
                .Include(x => x.HetRentalRequest)
                    .ThenInclude(y => y.RentalRequestStatusType)
                .Include(x => x.HetRentalRequest)
                    .ThenInclude(y => y.HetRentalRequestRotationList)
                .Include(x => x.HetRentalRequest)
                    .ThenInclude(y => y.LocalArea)
                .Include(x => x.HetRentalAgreement)
                    .ThenInclude(y => y.Equipment)
                        .ThenInclude(z => z.DistrictEquipmentType)
                .Include(x => x.HetRentalAgreement)
                    .ThenInclude(y => y.RentalAgreementStatusType)
                .Include(x => x.HetRentalAgreement)
                    .ThenInclude(y => y.Equipment)
                        .ThenInclude(z => z.LocalArea)
                .FirstOrDefault(a => a.ProjectId == projectId);

            if (project != null)
            {
                project.Status = project.ProjectStatusType.ProjectStatusTypeCode;

                // calculate the number of hired (yes or forced hire) equipment
                // count active requests (In Progress)
                int countActiveRequests = 0;

                foreach (HetRentalRequest rentalRequest in project.HetRentalRequest)
                {
                    rentalRequest.Status = rentalRequest.RentalRequestStatusType.RentalRequestStatusTypeCode;

                    int temp = 0;

                    foreach (HetRentalRequestRotationList equipment in rentalRequest.HetRentalRequestRotationList)
                    {
                        if (equipment.OfferResponse != null &&
                            equipment.OfferResponse.ToLower().Equals("yes"))
                        {
                            temp++;
                        }

                        if (equipment.IsForceHire != null &&
                            equipment.IsForceHire == true)
                        {
                            temp++;
                        }
                    }

                    rentalRequest.YesCount = temp;
                    rentalRequest.HetRentalRequestRotationList = null;

                    if (rentalRequest.RentalRequestStatusType.RentalRequestStatusTypeCode == null ||
                        rentalRequest.RentalRequestStatusType.RentalRequestStatusTypeCode
                            .Equals(HetRentalRequest.StatusInProgress))
                    {
                        countActiveRequests++;
                    }
                }

                // count active agreements (Active)
                int countActiveAgreements = 0;

                foreach (HetRentalAgreement rentalAgreement in project.HetRentalAgreement)
                {
                    rentalAgreement.Status = rentalAgreement.RentalAgreementStatusType.RentalAgreementStatusTypeCode;

                    if (rentalAgreement.RentalAgreementStatusType.RentalAgreementStatusTypeCode == null ||
                        rentalAgreement.RentalAgreementStatusType.RentalAgreementStatusTypeCode
                            .Equals(HetRentalAgreement.StatusActive))
                    {
                        countActiveAgreements++;
                    }

                    // workaround for converted records from Bc Bid
                    if (rentalAgreement.Number.StartsWith("BCBid"))
                    {
                        rentalAgreement.RentalRequestId = -1;
                        rentalAgreement.RentalRequestRotationListId = -1;
                    }

                    if (rentalAgreement.Equipment.LocalArea != null)
                    {
                        rentalAgreement.LocalAreaName = rentalAgreement.Equipment.LocalArea.Name;
                    }
                }

                foreach (HetRentalRequest rentalRequest in project.HetRentalRequest)
                {
                    if (rentalRequest.LocalArea != null)
                    {
                        rentalRequest.LocalAreaName = rentalRequest.LocalArea.Name;
                    }
                }

                //To make rental agreement lightweight
                foreach (HetRentalAgreement rentalAgreement in project.HetRentalAgreement)
                {
                    rentalAgreement.Equipment.LocalArea = null;
                }

                //To make rental request lightweight
                foreach (HetRentalRequest rentalRequest in project.HetRentalRequest)
                {
                    rentalRequest.LocalArea = null;
                }

                // Only allow editing the "Status" field under the following conditions:
                // * If Project.status is currently "Active" AND                
                //   (All child RentalRequests.Status != "In Progress" AND All child RentalAgreement.status != "Active"))
                // * If Project.status is currently != "Active"                               
                if (project.ProjectStatusType.ProjectStatusTypeCode.Equals(HetProject.StatusActive) &&
                    (countActiveRequests > 0 || countActiveAgreements > 0))
                {
                    project.CanEditStatus = false;
                }
                else
                {
                    project.CanEditStatus = true;
                }
            }

            // get fiscal year
            if (districtId > 0)
            {
                HetDistrictStatus status = _dbContext.HetDistrictStatus.AsNoTracking()
                    .First(x => x.DistrictId == districtId);

                int? fiscalYear = status.CurrentFiscalYear;

                // fiscal year in the status table stores the "start" of the year
                if (fiscalYear != null && project != null)
                {
                    DateTime fiscalYearStart = new DateTime((int)fiscalYear, 4, 1);
                    project.FiscalYearStartDate = fiscalYearStart;
                }
            }

            return _mapper.Map<ProjectDto>(project);
        }

        /// <summary>
        /// Convert to Project Lite Model
        /// </summary>
        /// <param name="project"></param>
        public ProjectLite ToLiteModel(HetProject project)
        {
            ProjectLite projectLite = new ProjectLite();

            if (project != null)
            {
                projectLite.Id = project.ProjectId;
                projectLite.Status = project.ProjectStatusType?.Description;
                projectLite.Name = project.Name;
                projectLite.PrimaryContact = _mapper.Map<ContactDto>(project.PrimaryContact);
                projectLite.District = _mapper.Map<DistrictDto>(project.District);
                projectLite.Requests = project.HetRentalRequest?.Count;
                projectLite.Hires = project.HetRentalAgreement?.Count;
                projectLite.ProvincialProjectNumber = project.ProvincialProjectNumber;
                projectLite.FiscalYear = project.FiscalYear;
            }

            return projectLite;
        }
    }
}
