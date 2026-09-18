using AmlScreening.Application.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace AmlScreening.Portal.ViewModels;


public sealed class UsersPageViewModel
{
    public IReadOnlyList<UserListItem> Users { get; set; }
        = Array.Empty<UserListItem>();

    public IReadOnlyList<PartnerOption> Partners { get; set; }
        = Array.Empty<PartnerOption>();

    public IReadOnlyList<BranchOption> Branches { get; set; }
        = Array.Empty<BranchOption>();


    public int? PartnerId { get; set; }

    public int? BranchId { get; set; }


    public int TotalUsers { get; set; }

    public int ActiveUsers { get; set; }

    public int InactiveUsers { get; set; }


    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;

    public int TotalPages { get; set; } = 1;


    public string? ErrorMessage { get; set; }


    public bool HasPreviousPage =>
        Page > 1;

    public bool HasNextPage =>
        Page < TotalPages;
}


// ============================================================
// CREATE PARTNER
// ============================================================

public sealed class CreatePartnerViewModel
{
    [Required]
    [StringLength(
        30,
        MinimumLength = 2)]
    [Display(Name = "Partner code")]
    public string PartnerCode { get; set; }
        = string.Empty;


    [Required]
    [StringLength(150)]
    [Display(Name = "Partner name")]
    public string PartnerName { get; set; }
        = string.Empty;


    [StringLength(500)]
    public string? Address { get; set; }


    [EmailAddress]
    [StringLength(250)]
    public string? Email { get; set; }


    [StringLength(50)]
    public string? PhoneNumber { get; set; }


    public bool IsActive { get; set; }
        = true;
}


// ============================================================
// CREATE BRANCH
// ============================================================

public sealed class CreateBranchViewModel
{
    [Range(
        1,
        int.MaxValue,
        ErrorMessage =
            "Select a partner.")]
    public int PartnerId { get; set; }


    [Required]
    [StringLength(150)]
    [Display(Name = "Branch name")]
    public string BranchName { get; set; }
        = string.Empty;


    [StringLength(500)]
    public string? Address { get; set; }


    [EmailAddress]
    [StringLength(250)]
    public string? Email { get; set; }


    [StringLength(50)]
    public string? PhoneNumber { get; set; }


    public bool IsActive { get; set; }
        = true;


    [BindNever]
    [ValidateNever]
    public IReadOnlyList<PartnerOption>
        Partners
    { get; set; }
        = Array.Empty<PartnerOption>();
}


// ============================================================
// CREATE USER
// ============================================================

public sealed class CreateUserViewModel
{
    [Range(
        1,
        int.MaxValue,
        ErrorMessage =
            "Select a partner.")]
    public int PartnerId { get; set; }


    [Range(
        1,
        int.MaxValue,
        ErrorMessage =
            "Select a branch.")]
    public int BranchId { get; set; }


    [Required]
    [StringLength(
        100,
        MinimumLength = 3)]
    public string Username { get; set; }
        = string.Empty;


    [EmailAddress]
    [StringLength(250)]
    public string? Email { get; set; }


    [StringLength(500)]
    public string? Address { get; set; }


    [StringLength(50)]
    public string? PhoneNumber { get; set; }


    [Required]
    [StringLength(
        100,
        MinimumLength = 8)]
    [DataType(DataType.Password)]
    public string Password { get; set; }
        = string.Empty;


    [Required]
    [DataType(DataType.Password)]
    [Compare(
        nameof(Password),
        ErrorMessage =
            "Passwords do not match.")]
    public string ConfirmPassword { get; set; }
        = string.Empty;


    public bool IsActive { get; set; }
        = true;


    public List<int> SelectedRoleIds { get; set; }
        = [];


    [BindNever]
    [ValidateNever]
    public IReadOnlyList<PartnerOption>
        Partners
    { get; set; }
        = Array.Empty<PartnerOption>();


    [BindNever]
    [ValidateNever]
    public IReadOnlyList<BranchOption>
        Branches
    { get; set; }
        = Array.Empty<BranchOption>();


    [BindNever]
    [ValidateNever]
    public IReadOnlyList<RoleOption>
        Roles
    { get; set; }
        = Array.Empty<RoleOption>();
}