namespace AmlScreening.Application.Models;

public sealed class PartnerPageResult
{
    public IReadOnlyList<PartnerListItem> Items { get; set; }
        = Array.Empty<PartnerListItem>();


    public int ActiveCount { get; set; }

    public int InactiveCount { get; set; }

    public int TotalUsers { get; set; }
    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages =>
        TotalCount <= 0
            ? 1
            : (int)Math.Ceiling((double)TotalCount / PageSize);
}