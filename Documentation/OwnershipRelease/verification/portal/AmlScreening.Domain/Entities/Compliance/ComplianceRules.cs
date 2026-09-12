using AmlScreening.Domain.Common;

namespace AmlScreening.Domain.Entities.Compliance;

public sealed class ComplianceRule : BaseEntity, IClientOwnedEntity
{
    public int ClientId { get; set; }

    public string RuleCode { get; set; } = string.Empty;

    public string RuleName { get; set; } = string.Empty;

    public string RuleType { get; set; } = string.Empty;

    public decimal? ThresholdAmount { get; set; }

    public string? Currency { get; set; }

    public int? LookbackMinutes { get; set; }

    public int? TransactionCount { get; set; }

    public int RiskScore { get; set; }

    public string Action { get; set; } = "Review";

    public int RuleVersion { get; set; } = 1;

    public int Priority { get; set; } = 100;

    public string ApprovalStatus { get; set; } = "Active";

    public string? ApprovedBy { get; set; }

    public DateTime? ApprovedDate { get; set; }

    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;

    public DateTime? EffectiveTo { get; set; }
}
