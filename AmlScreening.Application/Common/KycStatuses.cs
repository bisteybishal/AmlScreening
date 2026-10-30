namespace AmlScreening.Application.Common;

public static class KycScreeningStatuses
{
    public const string Processing =
        "PROCESSING";

    public const string NoMatch =
        "NO_MATCH";

    public const string PendingReview =
        "PENDING_REVIEW";

    public const string Completed =
        "COMPLETED";

    public const string Approved =
        "APPROVED";

    public const string Rejected =
        "REJECTED";

    public const string Failed =
        "FAILED";
}

public static class KycReviewStatuses
{
    public const string Pending =
        "PENDING";

    public const string Approved =
        "APPROVED";

    public const string Rejected =
        "REJECTED";
}