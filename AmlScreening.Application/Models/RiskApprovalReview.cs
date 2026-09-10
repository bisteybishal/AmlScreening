namespace AmlScreening.Application.Models;

// One score change, or the complete interdependent range set, is reviewed.
public sealed class RiskApprovalReview
{
    public int RuleId { get; init; }
    public string Kind { get; init; } = "Rules";
    public string? FactorCode { get; init; }
    public string Title { get; init; } = "Review risk changes";
    public string ReviewToken { get; init; } = "";
    public string? MakerName { get; init; }
    public bool CanApprove { get; init; }
    public string? ApprovalMessage { get; init; }
    public IReadOnlyList<RiskReviewRow> ActiveRows { get; init; } = [];
    public IReadOnlyList<RiskReviewRow> PendingRows { get; init; } = [];
}

public sealed record RiskReviewRow(string Value, string ScoreOrRange, string? Remarks, bool Removed);
