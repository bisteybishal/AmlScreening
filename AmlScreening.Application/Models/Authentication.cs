using System;
using System.Collections.Generic;
using System.Text;

namespace AmlScreening.Application.Models
{
    public sealed class loginuserData
    {
        public long UserId { get; set; }

        public int PartnerId { get; set; }

        public int BranchId { get; set; }

        public string Username { get; set; }
            = string.Empty;

        public string PasswordHash { get; set; }
            = string.Empty;

        public string PartnerName { get; set; }
            = string.Empty;
        public string PartnerCode { get; set; }
    = string.Empty;

        public string BranchName { get; set; }
            = string.Empty;

        public List<string> Roles { get; set; }
            = [];

        public List<string> Permissions { get; set; }
            = [];
    }
}
