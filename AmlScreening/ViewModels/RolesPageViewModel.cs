using AmlScreening.Application.Models;
using System.ComponentModel.DataAnnotations;

namespace AmlScreening.Portal.ViewModels;

public sealed class RolesPageViewModel
{
    public IReadOnlyList<RoleListItem> Roles { get; set; } = Array.Empty<RoleListItem>();
    public string? ErrorMessage { get; set; }
}

public sealed class CreateRoleViewModel
{
    [Required, StringLength(50, MinimumLength = 2)]
    [Display(Name = "Role name")]
    public string RoleName { get; set; } = string.Empty;

    [Display(Name = "Active role")]
    public bool IsActive { get; set; } = true;
}

public sealed class RolePermissionsViewModel
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public bool IsAdminRole { get; set; }
    public IReadOnlyList<PermissionListItem> Permissions { get; set; } = Array.Empty<PermissionListItem>();
    public List<int> SelectedPermissionIds { get; set; } = new();
}
