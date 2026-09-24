using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace AmlScreening.Portal.ViewModels;

public sealed class EditUserViewModel
{
    [Range(typeof(long), "1", "9223372036854775807")]
    public long UserId { get; set; }

    [BindNever, ValidateNever]
    public int PartnerId { get; set; }

    [BindNever, ValidateNever]
    public int BranchId { get; set; }

    [BindNever, ValidateNever]
    public string Username { get; set; } = string.Empty;

    [BindNever, ValidateNever]
    public string PartnerName { get; set; } = string.Empty;

    [BindNever, ValidateNever]
    public string BranchName { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Address { get; set; }

    [EmailAddress]
    [StringLength(250)]
    public string? Email { get; set; }

    [StringLength(50)]
    [Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    public bool IsActive { get; set; }
}