using AmlScreening.Domain.Entities.Compliance;

namespace AmlScreening.Application.Models;

public sealed class ComplianceRuleReview
{
    public ComplianceRule? ActiveRule { get; init; }

    public ComplianceRule PendingRule { get; init; }
        = default!;

    public bool CanApprove { get; init; }

    public string? ApprovalMessage { get; init; }

    public string ReviewToken { get; init; }
        = string.Empty;
}