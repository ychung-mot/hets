﻿using System;
using Newtonsoft.Json;

namespace HetsData.Model
{
    public partial class HetOwnerStatusType
    {
        [JsonProperty("Id")]
        public int OwnerStatusTypeId { get; set; }

        public string OwnerStatusTypeCode { get; set; }
        public string Description { get; set; }
        public string ScreenLabel { get; set; }
        public int? DisplayOrder { get; set; }
        public bool? IsActive { get; set; }
        public int ConcurrencyControlNumber { get; set; }
        public DateTime AppCreateTimestamp { get; set; }
        public string AppCreateUserid { get; set; }
        public string AppCreateUserGuid { get; set; }
        public string AppCreateUserDirectory { get; set; }
        public DateTime AppLastUpdateTimestamp { get; set; }
        public string AppLastUpdateUserid { get; set; }
        public string AppLastUpdateUserGuid { get; set; }
        public string AppLastUpdateUserDirectory { get; set; }
        public DateTime DbCreateTimestamp { get; set; }
        public string DbCreateUserId { get; set; }
        public DateTime DbLastUpdateTimestamp { get; set; }
        public string DbLastUpdateUserId { get; set; }
    }
}
