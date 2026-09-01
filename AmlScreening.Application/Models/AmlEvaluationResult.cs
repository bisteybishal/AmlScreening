using AmlScreening.Domain.Entities.Compliance;

namespace AmlScreening.Application.Models;

public sealed class AmlEvaluationResult
{
    public required AmlTransaction Transaction { get; init; }
    public required RiskAssessment RiskAssessment { get; init; }
    public required AmlDecision Decision { get; init; }
    public IReadOnlyList<ScreeningMatch> Matches { get; init; } = Array.Empty<ScreeningMatch>();
    public ComplianceAlert? Alert { get; init; }
    public ComplianceCase? Case { get; init; }
}

public sealed class AmlTransactionDetails
{
    public required AmlTransaction Transaction { get; init; }
    public Customer? Customer { get; init; }
    public RiskAssessment? RiskAssessment { get; init; }
    public AmlDecision? Decision { get; init; }
    public IReadOnlyList<ScreeningMatch> Matches { get; init; } = Array.Empty<ScreeningMatch>();
}
