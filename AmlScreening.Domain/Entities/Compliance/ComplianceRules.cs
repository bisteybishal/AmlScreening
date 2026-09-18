using AmlScreening.Domain.Common;

namespace AmlScreening.Domain.Entities.Compliance;

public sealed class ComplianceRule : BaseEntity, IPartnerOwnedEntity
{
    public int PartnerId { get; set; }

    public string RuleCode { get; set; } = string.Empty;

    public string RuleName { get; set; } = string.Empty;

    public string RuleType { get; set; } = string.Empty;

    public decimal? ThresholdAmount { get; set; }

    public string? Currency { get; set; }

    public int? LookbackMinutes { get; set; }

    public int? TransactionCount { get; set; }

    public int RiskScore { get; set; }

    public string Action { get; set; } = "Review";
    //version
    public int RuleVersion { get; set; } = 1;
    // Maker Checker
    public string ApprovalStatus { get; set; } = "PENDING";
    public long? MakerUserId { get; set; }

    public long? ApprovedByUserId { get; set; }

    public int Priority { get; set; } = 100;


    public string? ApprovedBy { get; set; }

    public DateTime? ApprovedDate { get; set; }

    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;

    public DateTime? EffectiveTo { get; set; }
}
