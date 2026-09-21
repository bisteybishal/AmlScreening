namespace AmlScreening.Application.Models;

public sealed class UserListItem
{
    public long UserId { get; set; }

    public int PartnerId { get; set; }

    public string PartnerCode { get; set; }
        = string.Empty;

    public string PartnerName { get; set; }
        = string.Empty;

    public int BranchId { get; set; }

    public string BranchName { get; set; }
        = string.Empty;

    public string Username { get; set; }
        = string.Empty;

    public string RoleName { get; set; }
        = string.Empty;

    public string? Address { get; set; }

    public string? Email { get; set; }

    public string? PhoneNumber { get; set; }

    public bool IsActive { get; set; }

    public DateTime? LastLoginDate { get; set; }

    public DateTime CreatedDate { get; set; }
}


public sealed class PartnerOption
{
    public int PartnerId { get; set; }

    public string PartnerCode { get; set; }
        = string.Empty;

    public string PartnerName { get; set; }
        = string.Empty;
}


public sealed class BranchOption
{
    public int BranchId { get; set; }

    public int PartnerId { get; set; }

    public string BranchName { get; set; }
        = string.Empty;
}


public sealed class RoleOption
{
    public int RoleId { get; set; }

    public string RoleName { get; set; }
        = string.Empty;
}