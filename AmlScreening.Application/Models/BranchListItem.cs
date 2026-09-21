namespace AmlScreening.Application.Models;

public sealed class BranchListItem
{
    public int BranchId { get; set; }

    public int PartnerId { get; set; }

    public string BranchName { get; set; }
        = string.Empty;

    public string? Address { get; set; }

    public string? Email { get; set; }

    public string? PhoneNumber { get; set; }

    public bool IsActive { get; set; }

    public int UserCount { get; set; }

    public DateTime CreatedDate { get; set; }
}