using System.ComponentModel.DataAnnotations;

namespace AmlScreening.Portal.ViewModels;

public sealed class EditPartnerViewModel
{
    [Range(1, int.MaxValue)]
    public int PartnerId { get; set; }

    [Required]
    [StringLength(30, MinimumLength = 2)]
    [Display(Name = "Partner code")]
    public string PartnerCode { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    [Display(Name = "Partner name")]
    public string PartnerName { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Address { get; set; }
    [StringLength(50)]
    public string? Country { get; set; }

    [StringLength(50)]
    public string? City { get; set; }

    [EmailAddress]
    [StringLength(250)]
    public string? Email { get; set; }

    [StringLength(50)]
    [Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    public bool IsActive { get; set; }
}