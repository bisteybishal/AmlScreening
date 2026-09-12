using System;
using System.Collections.Generic;
using System.Text;

namespace AmlScreening.Domain.Entities
{
    public class SubscriptionPlan
    {
        public int PlanId { get; set; }

        public string SourceCode { get; set; } = string.Empty;

        public string SourceName { get; set; } = string.Empty;

        public int DurationMonths { get; set; }

        public decimal Rate { get; set; }

        public bool IsActive { get; set; }

        public string DeleteFg { get; set; } = "N";
        public DateTime? DeleteDate { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime? ExpireDate { get; set; }
        public string SubscriptionKey { get; set; } = string.Empty;
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }
}
