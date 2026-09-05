using AmlScreening.Domain.Common;

namespace AmlScreening.Domain.Entities.Compliance;
public sealed class RiskFactor : BaseEntity, IClientOwnedEntity
{
    public int ClientId { get; set; }

    public string FactorCode { get; set; } = string.Empty;

    public string FactorName { get; set; } = string.Empty;

    public string ScoreCategory { get; set; } = string.Empty;

    public string ValueType { get; set; } = string.Empty;

    public decimal WeightPercent { get; set; }

    public int DisplayOrder { get; set; }
}

public sealed class RiskFactorRule : BaseEntity, IClientOwnedEntity
{
    public int ClientId { get; set; }

    public int RiskFactorId { get; set; }

    public string? ValueCode { get; set; }

    public string? ValueName { get; set; }

    public decimal? MinimumValue { get; set; }

    public decimal? MaximumValue { get; set; }

    public int Score { get; set; }

    public string? Remarks { get; set; }

    public int RuleVersion { get; set; } = 1;

    public string ApprovalStatus { get; set; } = "DRAFT";

    public string? ApprovedBy { get; set; }

    public DateTime? ApprovedDate { get; set; }

    public DateTime EffectiveFrom { get; set; } =
        DateTime.UtcNow;

    public DateTime? EffectiveTo { get; set; }
}

public sealed class RiskBand : BaseEntity, IClientOwnedEntity
{
    public int ClientId { get; set; }

    public string RiskLevel { get; set; } = string.Empty;

    public int MinimumScore { get; set; }

    public int? MaximumScore { get; set; }

    public string Decision { get; set; } = "ALLOW";

    public string? Remarks { get; set; }

    public int BandVersion { get; set; } = 1;

    public string ApprovalStatus { get; set; } = "DRAFT";

    public string? ApprovedBy { get; set; }

    public DateTime? ApprovedDate { get; set; }
}

public sealed class RiskModel : BaseEntity, IClientOwnedEntity
{
    public int ClientId { get; set; }

    public string ModelCode { get; set; } = "AML-RISK";

    public int ModelVersion { get; set; } = 1;

    public decimal CustomerWeightPercent { get; set; } = 40;

    public decimal TransactionWeightPercent { get; set; } = 40;

    public decimal ScreeningWeightPercent { get; set; } = 20;

    public string ApprovalStatus { get; set; } = "DRAFT";

    public string? ApprovedBy { get; set; }

    public DateTime? ApprovedDate { get; set; }

    public DateTime EffectiveFrom { get; set; } =
        DateTime.UtcNow;

    public DateTime? EffectiveTo { get; set; }
}

public sealed class RiskAssessmentFactor
    : BaseEntity, IClientOwnedEntity
{
    public int ClientId { get; set; }

    public int RiskAssessmentId { get; set; }

    public string FactorCode { get; set; } = string.Empty;

    public string InputValue { get; set; } = string.Empty;

    public int RawScore { get; set; }

    public decimal WeightPercent { get; set; }

    public decimal WeightedScore { get; set; }

    public string Explanation { get; set; } = string.Empty;

    public int RuleVersion { get; set; }
}
