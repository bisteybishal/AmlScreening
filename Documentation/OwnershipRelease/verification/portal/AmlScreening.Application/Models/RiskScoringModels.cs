using AmlScreening.Domain.Entities.Compliance;

namespace AmlScreening.Application.Models;

public sealed class RiskScoringInput
{
    public bool IsPep { get; init; }
    public string? Occupation { get; init; }
    public string? SourceOfFunds { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string? ReceivingCountry { get; init; }
    public string? PurposeCode { get; init; }
    public int RegisteredReceiverCount { get; init; }
    public string? RelationshipCode { get; init; }
    public int ScreeningRiskScore { get; init; }
    public int AdditionalTransactionRiskScore { get; init; }
}

public sealed class RiskFactorResult
{
    public required string FactorCode { get; init; }
    public required string FactorName { get; init; }
    public required string ScoreCategory { get; init; }
    public required string InputValue { get; init; }
    public int RawScore { get; init; }
    public decimal WeightPercent { get; init; }
    public decimal WeightedScore { get; init; }
    public int RuleVersion { get; init; }
    public required string Explanation { get; init; }

}

public sealed class RiskScoringResult
{
    public int CustomerRiskScore { get; init; }
    public int TransactionRiskScore { get; init; }
    public int ScreeningRiskScore { get; init; }
    public int OverallRiskScore { get; init; }
    public required string RiskLevel { get; init; }
    public required string RecommendedDecision { get; init; }
    public required string ModelVersion { get; init; }
    public required IReadOnlyList<RiskFactorResult> Factors { get; init; }
}

public sealed class RiskScoringSetupResult
{
    public bool CanEditRules { get; init; }
    public bool CanEditBands { get; init; }
    public bool HasRuleDraft { get; init; }
    public bool HasBandDraft { get; init; }
    public RiskModel? ActiveModel { get; init; }
    public IReadOnlyList<RiskFactor> Factors { get; init; } = Array.Empty<RiskFactor>();
    public RiskFactor? SelectedFactor { get; init; }
    public IReadOnlyList<RiskFactorRule> Rules { get; init; } = Array.Empty<RiskFactorRule>();
    public IReadOnlyList<RiskBand> Bands { get; init; } = Array.Empty<RiskBand>();
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public int TotalCount { get; init; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}
