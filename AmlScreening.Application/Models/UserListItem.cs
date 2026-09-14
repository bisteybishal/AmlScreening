namespace AmlScreening.Application.Models;

public sealed class UserListItem
{
    public long UserId { get; set; }
    public int ClientId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public bool HasAdminRole { get; set; }   
    public bool IsActive { get; set; }
    public DateTime? LastLoginDate { get; set; }
    public DateTime CreatedDate { get; set; }
}

public sealed class RoleOption
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
}
