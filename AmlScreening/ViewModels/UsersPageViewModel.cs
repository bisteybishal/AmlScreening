using AmlScreening.Application.Models;
using System.ComponentModel.DataAnnotations;

namespace AmlScreening.Portal.ViewModels;

public sealed class UsersPageViewModel
{
    public IReadOnlyList<UserListItem> Users { get; set; } = Array.Empty<UserListItem>();
    public string? ErrorMessage { get; set; }
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int InactiveUsers { get; set; }
    public int Administrators { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalPages { get; set; } = 1;
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

public sealed class CreateUserViewModel
{
    [Required]
    [StringLength(100, MinimumLength = 3)]
    [RegularExpression(@"^[a-zA-Z0-9._-]+$", ErrorMessage = "Use letters, numbers, dot, underscore, or hyphen only.")]
    public string Username { get; set; } = string.Empty;


    [Required]
    [StringLength(200)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;


    [Required]
    [StringLength(150)]
    [Display(Name = "Company name")]
    public string CompanyName { get; set; } = string.Empty;


    [Required]
    [EmailAddress]
    [StringLength(200)]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;


    [StringLength(300)]
    [Display(Name = "Address")]
    public string Address { get; set; } = string.Empty;


    [Phone]
    [StringLength(30)]
    [Display(Name = "Phone number")]
    public string PhoneNumber { get; set; } = string.Empty;


    [Required]
    [StringLength(100, MinimumLength = 8)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;


    [Display(Name = "Active user")]
    public bool IsActive { get; set; } = true;
    [Display(Name = "Roles")]
    public List<int> SelectedRoleIds { get; set; } = new();
    public IReadOnlyList<RoleOption> Roles { get; set; } = Array.Empty<RoleOption>();
    public int ClientId { get; set; }

}

public sealed class UserAccessViewModel
{
    public long UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public IReadOnlyList<RoleOption> Roles { get; set; }= Array.Empty<RoleOption>();
    public List<int> SelectedRoleIds { get; set; }= new();
    public string RoleNames { get; set; }= string.Empty;
    public IReadOnlyList<PermissionListItem> Permissions { get; set; } = Array.Empty<PermissionListItem>();
    public List<int> SelectedPermissionIds { get; set; } = new();

    public IReadOnlyList<RoleAccessViewModel> RoleAccess { get; set; }
       = Array.Empty<RoleAccessViewModel>();
}

public sealed class RoleAccessViewModel
{
    public int RoleId { get; set; }

    public string RoleName { get; set; } = string.Empty;

    public bool IsSelected { get; set; }

    public IReadOnlyList<PermissionListItem> Permissions { get; set; }
        = Array.Empty<PermissionListItem>();
}
