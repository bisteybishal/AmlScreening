using AmlScreening.Domain.Entities.Compliance;

namespace AmlScreening.Portal.ViewModels;

public sealed class ComplianceRuleViewModel
{
    public int Id { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public string RuleCode { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty;
    public decimal? ThresholdAmount { get; set; }
    public string? Currency { get; set; }
    public int? LookbackMinutes { get; set; }
    public int? TransactionCount { get; set; }
    public int RiskScore { get; set; }
    public string Action { get; set; } = string.Empty;
    public int RuleVersion { get; set; } = 1;
    public int Priority { get; set; } = 100;
    public string ApprovalStatus { get; set; } = "PENDING";
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; }

    public int PartnerId { get; set; }

    public long? MakerUserId { get; set; }

    public long? ApprovedByUserId { get; set; }

    public string? ApprovedBy { get; set; }

    public DateTime? ApprovedDate { get; set; }

    public bool CanEdit { get; set; }

    public bool CanApprove { get; set; }

    public bool CanDelete { get; set; }

   
}
public sealed class ComplianceRuleReviewViewModel
{
    public ComplianceRule? ActiveRule { get; set; }

    public ComplianceRule PendingRule { get; set; }
        = default!;

    public bool CanApprove { get; set; }

    public string? ApprovalMessage { get; set; }
}