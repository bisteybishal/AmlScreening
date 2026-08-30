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
    }
}
