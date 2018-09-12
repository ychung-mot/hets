﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Microsoft.EntityFrameworkCore;
using Hangfire.Console;
using Hangfire.Server;
using Hangfire.Console.Progress;
using HetsData.Helpers;
using HetsData.Model;

namespace HetsImport.Import
{
    /// <summary>
    /// Import Equipment Usage Records
    /// </summary>
    public static class ImportEquipUsage
    {
        public const string OldTable = "Equip_Usage";
        public const string NewTable = "HET_TIME_RECORD";
        public const string XmlFileName = "Equip_Usage.xml";

        /// <summary>
        /// Progress Property
        /// </summary>
        public static string OldTableProgress => OldTable + "_Progress";

        /// <summary>
        /// Fix the sequence for the tables populated by the import process
        /// </summary>
        /// <param name="performContext"></param>
        /// <param name="dbContext"></param>
        public static void ResetSequence(PerformContext performContext, DbAppContext dbContext)
        {
            try
            {
                // **************************************
                // Time Records
                // **************************************
                performContext.WriteLine("*** Resetting HET_TIME_RECORD database sequence after import ***");
                Debug.WriteLine("Resetting HET_TIME_RECORD database sequence after import");

                if (dbContext.HetTimeRecord.Any())
                {
                    // get max key
                    int maxKey = dbContext.HetTimeRecord.Max(x => x.TimeRecordId);
                    maxKey = maxKey + 1;

                    using (DbCommand command = dbContext.Database.GetDbConnection().CreateCommand())
                    {
                        // check if this code already exists
                        command.CommandText = string.Format(@"ALTER SEQUENCE public.""HET_TIME_RECORD_TIME_RECORD_ID_seq"" RESTART WITH {0};", maxKey);

                        dbContext.Database.OpenConnection();
                        command.ExecuteNonQuery();
                        dbContext.Database.CloseConnection();
                    }
                }

                performContext.WriteLine("*** Done resetting HET_TIME_RECORD database sequence after import ***");
                Debug.WriteLine("Resetting HET_TIME_RECORD database sequence after import - Done!");
                
                // **************************************
                // Rental Agreements
                // **************************************
                performContext.WriteLine("*** Resetting HET_RENTAL_AGREEMENT database sequence after import ***");
                Debug.WriteLine("Resetting HET_RENTAL_AGREEMENT database sequence after import");

                if (dbContext.HetRentalAgreement.Any())
                {
                    // get max key
                    int maxKey = dbContext.HetRentalAgreement.Max(x => x.RentalAgreementId);
                    maxKey = maxKey + 1;

                    using (DbCommand command = dbContext.Database.GetDbConnection().CreateCommand())
                    {
                        // check if this code already exists
                        command.CommandText = string.Format(@"ALTER SEQUENCE public.""HET_RENTAL_AGREEMENT_RENTAL_AGREEMENT_ID_seq"" RESTART WITH {0};", maxKey);

                        dbContext.Database.OpenConnection();
                        command.ExecuteNonQuery();
                        dbContext.Database.CloseConnection();
                    }
                }

                performContext.WriteLine("*** Done resetting HET_RENTAL_AGREEMENT database sequence after import ***");
                Debug.WriteLine("Resetting HET_RENTAL_AGREEMENT database sequence after import - Done!");
            }
            catch (Exception e)
            {
                performContext.WriteLine("*** ERROR ***");
                performContext.WriteLine(e.ToString());
                throw;
            }
        }

        /// <summary>
        /// Import Equipment Usage
        /// </summary>
        /// <param name="performContext"></param>
        /// <param name="dbContext"></param>
        /// <param name="fileLocation"></param>
        /// <param name="systemId"></param>
        public static void Import(PerformContext performContext, DbAppContext dbContext, string fileLocation, string systemId)
        {
            // check the start point. If startPoint ==  sigId then it is already completed
            int startPoint = ImportUtility.CheckInterMapForStartPoint(dbContext, OldTableProgress, BcBidImport.SigId, NewTable);

            if (startPoint == BcBidImport.SigId)   // this means the import job completed for all the records in this file
            {
                performContext.WriteLine("*** Importing " + XmlFileName + " is complete from the former process ***");
                return;
            }

            int maxTimeSheetIndex = 0;

            if (dbContext.HetRentalAgreement.Any())
            {
                maxTimeSheetIndex = dbContext.HetRentalAgreement.Max(x => x.RentalAgreementId);
            }

            try
            {           
                string rootAttr = "ArrayOf" + OldTable;

                // create progress indicator
                performContext.WriteLine("Processing " + OldTable);
                IProgressBar progress = performContext.WriteProgressBar();
                progress.SetValue(0);

                // create serializer and serialize xml file
                XmlSerializer ser = new XmlSerializer(typeof(ImportModels.EquipUsage[]), new XmlRootAttribute(rootAttr));
                MemoryStream memoryStream = ImportUtility.MemoryStreamGenerator(XmlFileName, OldTable, fileLocation, rootAttr);
                ImportModels.EquipUsage[] legacyItems = (ImportModels.EquipUsage[])ser.Deserialize(memoryStream);                
                
                int ii = startPoint;

                // skip the portion already processed
                if (startPoint > 0)    
                {
                    legacyItems = legacyItems.Skip(ii).ToArray();
                }

                Debug.WriteLine("Importing TimeSheet Data. Total Records: " + legacyItems.Length);

                foreach (ImportModels.EquipUsage item in legacyItems.WithProgress(progress))
                {
                    // see if we have this one already
                    string oldProjectKey = item.Project_Id.ToString();
                    string oldEquipKey = item.Project_Id.ToString();
                    string oldCreatedDate = item.Created_Dt;

                    string oldKey = string.Format("{0}-{1}-{2}", oldProjectKey, oldEquipKey, oldCreatedDate);
                    
                    HetImportMap importMap = dbContext.HetImportMap
                        .FirstOrDefault(x => x.OldTable == OldTable && 
                                             x.OldKey == oldKey);

                    // new entry
                    if (importMap == null && item.Equip_Id > 0)
                    {
                        HetTimeRecord instance = null;
                        CopyToTimeRecorded(dbContext, item, ref instance, systemId, ref maxTimeSheetIndex);

                        if (instance != null)
                        {
                            ImportUtility.AddImportMap(dbContext, OldTable, oldKey, NewTable, instance.TimeRecordId);
                        }
                    }

                    // save change to database periodically to avoid frequent writing to the database
                    if (ii++ % 1000 == 0)
                    {
                        try
                        {
                            ImportUtility.AddImportMapForProgress(dbContext, OldTableProgress, ii.ToString(), BcBidImport.SigId, NewTable);
                            dbContext.SaveChangesForImport();
                        }
                        catch (Exception e)
                        {
                            performContext.WriteLine("Error saving data " + e.Message);
                        }
                    }
                }

                try
                {
                    performContext.WriteLine("*** Importing " + XmlFileName + " is Done ***");
                    ImportUtility.AddImportMapForProgress(dbContext, OldTableProgress, BcBidImport.SigId.ToString(), BcBidImport.SigId, NewTable);
                    dbContext.SaveChangesForImport();
                }
                catch (Exception e)
                {
                    string temp = string.Format("Error saving data (RentalAgreementIndex: {0}): {1}", maxTimeSheetIndex, e.Message);
                    performContext.WriteLine(temp);
                    throw new DataException(temp);
                }
            }
            catch (Exception e)
            {
                performContext.WriteLine("*** ERROR ***");
                performContext.WriteLine(e.ToString());
                throw;
            }
        }

        /// <summary>
        /// Map data
        /// </summary>
        /// <param name="dbContext"></param>
        /// <param name="oldObject"></param>
        /// <param name="timeRecord"></param>
        /// <param name="systemId"></param>
        /// <param name="maxTimeSheetIndex"></param>
        private static void CopyToTimeRecorded(DbAppContext dbContext, ImportModels.EquipUsage oldObject, 
            ref HetTimeRecord timeRecord, string systemId, ref int maxTimeSheetIndex)
        {
            try
            {
                if (oldObject.Equip_Id <= 0)
                {
                    return;
                }

                if (oldObject.Project_Id <= 0)
                {
                    return;
                }

                // ***********************************************
                // we only need records from the current fiscal
                // so ignore all others
                // ***********************************************
                DateTime fiscalStart;
                DateTime fiscalEnd;                

                if (DateTime.UtcNow.Month == 1 || DateTime.UtcNow.Month == 2 || DateTime.UtcNow.Month == 3)
                {
                    fiscalEnd = new DateTime(DateTime.UtcNow.Year, 3, 31);
                }
                else
                {
                    fiscalEnd = new DateTime(DateTime.UtcNow.AddYears(1).Year, 3, 31);
                }

                if (DateTime.UtcNow.Month == 1 || DateTime.UtcNow.Month == 2 || DateTime.UtcNow.Month == 3)
                {
                    fiscalStart = new DateTime(DateTime.UtcNow.AddYears(-1).Year, 4, 1);
                }
                else
                {
                    fiscalStart = new DateTime(DateTime.UtcNow.Year, 4, 1);
                }

                string tempRecordDate = oldObject.Worked_Dt;

                if (string.IsNullOrEmpty(tempRecordDate))
                {
                    return; // ignore if we don't have a created date
                }                

                if (!string.IsNullOrEmpty(tempRecordDate))
                {
                    DateTime? recordDate = ImportUtility.CleanDateTime(tempRecordDate);

                    if (recordDate == null ||
                        recordDate < fiscalStart ||
                        recordDate > fiscalEnd)
                    {
                        return; // ignore this record - it is outside of the current fiscal year
                    }
                }

                
                // ************************************************
                // get the imported equipment record map
                // ************************************************
                string tempId = oldObject.Equip_Id.ToString();

                HetImportMap mapEquip = dbContext.HetImportMap.AsNoTracking()
                    .FirstOrDefault(x => x.OldKey == tempId &&
                                         x.OldTable == ImportEquip.OldTable &&
                                         x.NewTable == ImportEquip.NewTable);

                if (mapEquip == null)
                {
                    throw new DataException(string.Format("Cannot locate Equipment record (Time Sheet Equip Id: {0}", tempId));
                }

                // ***********************************************
                // find the equipment record
                // ***********************************************
                HetEquipment equipment = dbContext.HetEquipment.AsNoTracking()
                    .FirstOrDefault(x => x.EquipmentId == mapEquip.NewKey);

                if (equipment == null)
                {
                    throw new ArgumentException(string.Format("Cannot locate Equipment record (Time Sheet Equip Id: {0}", tempId));
                }

                // ************************************************
                // get the imported project record map
                // ************************************************
                string tempProjectId = oldObject.Project_Id.ToString();

                HetImportMap mapProject = dbContext.HetImportMap.AsNoTracking()
                    .FirstOrDefault(x => x.OldKey == tempProjectId &&
                                         x.OldTable == ImportProject.OldTable &&
                                         x.NewTable == ImportProject.NewTable);

                // ***********************************************
                // find the project record
                // (or create a project (inactive))
                // ***********************************************
                HetProject project;

                if (mapProject != null)
                {
                    project = dbContext.HetProject.AsNoTracking()
                        .FirstOrDefault(x => x.ProjectId == mapProject.NewKey);

                    if (project == null)
                    {
                        throw new ArgumentException(string.Format("Cannot locate Project record (Time Sheet Equip Id: {0}", tempId));
                    }
                }
                else
                {
                    int? statusId = StatusHelper.GetStatusId("Complete", "projectStatus", dbContext);

                    if (statusId == null)
                    {
                        throw new DataException(string.Format("Status Id cannot be null (Time Sheet Equip Id: {0}", tempId));
                    }                    

                    // create new project
                    project = new HetProject
                    {
                        Information = "Created to support Time Record import from BCBid",
                        ProjectStatusTypeId = (int)statusId,
                        Name = "Legacy BCBid Project",
                        AppCreateUserid = systemId,
                        AppCreateTimestamp = DateTime.UtcNow,
                        AppLastUpdateUserid = systemId,
                        AppLastUpdateTimestamp = DateTime.UtcNow
                    };

                    dbContext.HetProject.Add(project);

                    // save now so we can access it for other time records
                    dbContext.SaveChanges();

                    // add mapping record
                    ImportUtility.AddImportMapForProgress(dbContext, ImportProject.OldTable, tempProjectId, project.ProjectId, ImportProject.NewTable);
                    dbContext.SaveChanges();
                }

                // ***********************************************
                // find or create the rental agreement
                // ***********************************************
                DateTime? enteredDate = ImportUtility.CleanDateTime(oldObject.Entered_Dt); // use for the agreement

                HetRentalAgreement agreement = dbContext.HetRentalAgreement.AsNoTracking()
                    .FirstOrDefault(x => x.EquipmentId == equipment.EquipmentId &&
                                         x.ProjectId == project.ProjectId);

                if (agreement == null)
                {
                    // create a new agreement record
                    agreement = new HetRentalAgreement
                    {
                        EquipmentId = equipment.EquipmentId,
                        ProjectId = project.ProjectId,
                        Note = "Created to support Time Record import from BCBid",
                        Number = "Legacy BCBid Agreement",
                        DatedOn = enteredDate,
                        AppCreateUserid = systemId,
                        AppCreateTimestamp = DateTime.UtcNow,
                        AppLastUpdateUserid = systemId,
                        AppLastUpdateTimestamp = DateTime.UtcNow
                    };

                    if (project.HetRentalAgreement == null)
                    {
                        project.HetRentalAgreement = new List<HetRentalAgreement>();
                    }

                    project.HetRentalAgreement.Add(agreement);

                    // save now so we can access it for other time records
                    dbContext.SaveChangesForImport();
                }

                // ***********************************************
                // create time record
                // ***********************************************
                timeRecord = new HetTimeRecord { TimeRecordId = ++maxTimeSheetIndex };

                // ***********************************************
                // set time record attributes
                // ***********************************************
                DateTime? workedDate = ImportUtility.CleanDateTime(oldObject.Worked_Dt);

                if (workedDate != null)
                {
                    timeRecord.WorkedDate = (DateTime)workedDate;
                }
                else
                {
                    throw new DataException(string.Format("Worked Date cannot be null (Time Sheet Index: {0}", maxTimeSheetIndex));
                }

                // get hours worked
                float? tempHoursWorked = ImportUtility.GetFloatValue(oldObject.Hours);

                if (tempHoursWorked != null)
                {
                    timeRecord.Hours = tempHoursWorked;
                }
                else
                {
                    throw new DataException(string.Format("Hours cannot be null (Time Sheet Index: {0}", maxTimeSheetIndex));
                }                

                if (enteredDate != null)
                {
                    timeRecord.EnteredDate = (DateTime)enteredDate;
                }
                else
                {
                    throw new DataException(string.Format("Entered Date cannot be null (Time Sheet Index: {0}", maxTimeSheetIndex));
                }

                // ***********************************************
                // create time record
                // ***********************************************                            
                timeRecord.AppCreateUserid = systemId;
                timeRecord.AppCreateTimestamp = DateTime.UtcNow;
                timeRecord.AppLastUpdateUserid = systemId;
                timeRecord.AppLastUpdateTimestamp = DateTime.UtcNow;

                if (agreement.HetTimeRecord == null)
                {
                    agreement.HetTimeRecord = new List<HetTimeRecord>();
                }

                agreement.HetTimeRecord.Add(timeRecord);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("***Error*** - Worked Date: " + oldObject.Worked_Dt);
                Debug.WriteLine("***Error*** - Master Time Record Index: " + maxTimeSheetIndex);
                Debug.WriteLine(ex.Message);
                throw;
            }
        }        

        public static void Obfuscate(PerformContext performContext, DbAppContext dbContext, string sourceLocation, string destinationLocation, string systemId)
        {
            int startPoint = ImportUtility.CheckInterMapForStartPoint(dbContext, "Obfuscate_" + OldTableProgress, BcBidImport.SigId, NewTable);

            if (startPoint == BcBidImport.SigId)    // this means the import job it has done today is complete for all the records in the xml file.
            {
                performContext.WriteLine("*** Obfuscating " + XmlFileName + " is complete from the former process ***");
                return;
            }
            try
            {
                string rootAttr = "ArrayOf" + OldTable;

                // create progress indicator
                performContext.WriteLine("Processing " + OldTable);
                IProgressBar progress = performContext.WriteProgressBar();
                progress.SetValue(0);

                // create serializer and serialize xml file
                XmlSerializer ser = new XmlSerializer(typeof(ImportModels.EquipUsage[]), new XmlRootAttribute(rootAttr));
                MemoryStream memoryStream = ImportUtility.MemoryStreamGenerator(XmlFileName, OldTable, sourceLocation, rootAttr);
                ImportModels.EquipUsage[] legacyItems = (ImportModels.EquipUsage[])ser.Deserialize(memoryStream);

                performContext.WriteLine("Obfuscating EquipUsage data");
                progress.SetValue(0);

                foreach (ImportModels.EquipUsage item in legacyItems.WithProgress(progress))
                {
                    item.Created_By = systemId;                    
                }

                performContext.WriteLine("Writing " + XmlFileName + " to " + destinationLocation);

                // write out the array
                FileStream fs = ImportUtility.GetObfuscationDestination(XmlFileName, destinationLocation);
                ser.Serialize(fs, legacyItems);
                fs.Close();
            }
            catch (Exception e)
            {
                performContext.WriteLine("*** ERROR ***");
                performContext.WriteLine(e.ToString());
            }
        }
    }
}

