namespace AmlScreening.Application.Models;

public sealed class UserPageResult
{
    public IReadOnlyList<UserListItem> Items { get; init; }
       = Array.Empty<UserListItem>();

    public int TotalUsers { get; init; }

    public int ActiveUsers { get; init; }

    public int InactiveUsers { get; init; }

    //public int Administrators { get; init; }

    public int PageNumber { get; init; }

    public int PageSize { get; init; }

    public int TotalPages =>
        (int)Math.Ceiling(
            TotalUsers /
            (double)PageSize);
}
