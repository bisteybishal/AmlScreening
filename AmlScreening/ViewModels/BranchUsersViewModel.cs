using AmlScreening.Application.Models;

namespace AmlScreening.Portal.ViewModels;

public sealed class BranchUsersViewModel
{
    public int PartnerId { get; set; }

    public string PartnerName { get; set; }
        = string.Empty;

    public int BranchId { get; set; }

    public string BranchName { get; set; }
        = string.Empty;



    public IReadOnlyList<UserListItem> Users { get; set; }
        = Array.Empty<UserListItem>();

    public int TotalUsers { get; set; }

    public int ActiveUsers { get; set; }

    public int InactiveUsers { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;

    public int TotalPages { get; set; } = 1;
}