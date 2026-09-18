using System;
using System.Collections.Generic;
using System.Text;

namespace AmlScreening.Application.Models
{
    public sealed class ComplianceRuleInput
    {
        public int Id { get; set; }
        public string RuleCode { get; set; } = string.Empty;
        public string RuleName { get; set; } = string.Empty;
        public string RuleType { get; set; } = string.Empty;
        public decimal? ThresholdAmount { get; set; }
        public string? Currency { get; set; }
        public int? LookbackMinutes { get; set; }
        public int? TransactionCount { get; set; }
        public int RiskScore { get; set; }
        public string Action { get; set; } = "REVIEW";
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }
}
