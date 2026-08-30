using AmlScreening.Application.Models;
using System.ComponentModel.DataAnnotations;

namespace AmlScreening.Portal.ViewModels;

public sealed class UsersPageViewModel
{
    public IReadOnlyList<UserListItem> Users { get; set; } = Array.Empty<UserListItem>();
    public int TotalUsers => Users.Count;
    public int ActiveUsers => Users.Count(x => x.IsActive);
    public int InactiveUsers => Users.Count(x => !x.IsActive);
    public int Administrators => Users.Count(x => x.RoleName.Equals("Admin", StringComparison.OrdinalIgnoreCase));
}

public sealed class CreateUserViewModel
{
    [Required, StringLength(100, MinimumLength = 3)]
    [RegularExpression(@"^[a-zA-Z0-9._-]+$", ErrorMessage = "Use letters, numbers, dot, underscore, or hyphen only.")]
    public string Username { get; set; } = string.Empty;

    [Required, StringLength(200)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Select a role.")]
    [Display(Name = "Role")]
    public int RoleId { get; set; }

    [Display(Name = "Active user")]
    public bool IsActive { get; set; } = true;

    public IReadOnlyList<RoleOption> Roles { get; set; } = Array.Empty<RoleOption>();
}

public sealed class UserAccessViewModel
{
    public long UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public IReadOnlyList<PermissionListItem> Permissions { get; set; } = Array.Empty<PermissionListItem>();
    public List<int> SelectedPermissionIds { get; set; } = new();
}
