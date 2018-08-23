﻿namespace HetsApi.Models
{
    /// <summary>
    /// User Extension (to support authorization)
    /// </summary>
    public sealed partial class User
    {
        /// <summary>
        /// User Permission Claim Property
        /// </summary>
        public const string PermissionClaim = "permission_claim";

        /// <summary>
        /// UserId Claim Property
        /// </summary>
        public const string UseridClaim = "userid_claim";
    }
}
