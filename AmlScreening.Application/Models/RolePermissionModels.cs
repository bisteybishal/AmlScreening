public sealed class RoleListItem
{
    public int RoleId { get; set; }

    public string RoleName { get; set; }
        = string.Empty;

    public bool IsActive { get; set; }

    public DateTime CreatedDate { get; set; }

    public string? CreatedBy { get; set; }

    public int UserCount { get; set; }

    public int PermissionCount { get; set; }
}


public sealed class PermissionListItem
{
    public int PermissionId { get; set; }

    public string PermissionCode { get; set; }
        = string.Empty;

    public string PermissionName { get; set; }
        = string.Empty;

    public bool IsSelected { get; set; }
}