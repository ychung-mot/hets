﻿using System;
using System.Collections.Generic;

namespace HetsData.Model
{
    public partial class HetEquipmentType
    {
        public HetEquipmentType()
        {
            HetDistrictEquipmentType = new HashSet<HetDistrictEquipmentType>();
        }

        public int EquipmentTypeId { get; set; }
        public string Name { get; set; }
        public float? BlueBookRateNumber { get; set; }
        public float? BlueBookSection { get; set; }
        public float? ExtendHours { get; set; }
        public int NumberOfBlocks { get; set; }
        public float? MaximumHours { get; set; }
        public float? MaxHoursSub { get; set; }
        public DateTime DbCreateTimestamp { get; set; }
        public string AppCreateUserDirectory { get; set; }
        public DateTime DbLastUpdateTimestamp { get; set; }
        public string AppLastUpdateUserDirectory { get; set; }
        public bool IsDumpTruck { get; set; }
        public DateTime AppCreateTimestamp { get; set; }
        public string AppCreateUserGuid { get; set; }
        public string AppCreateUserid { get; set; }
        public DateTime AppLastUpdateTimestamp { get; set; }
        public string AppLastUpdateUserGuid { get; set; }
        public string AppLastUpdateUserid { get; set; }
        public string DbCreateUserId { get; set; }
        public string DbLastUpdateUserId { get; set; }
        public int ConcurrencyControlNumber { get; set; }

        public ICollection<HetDistrictEquipmentType> HetDistrictEquipmentType { get; set; }
    }
}
