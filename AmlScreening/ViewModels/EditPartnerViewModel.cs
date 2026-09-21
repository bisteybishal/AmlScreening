using System.ComponentModel.DataAnnotations;

namespace AmlScreening.Portal.ViewModels;

public sealed class EditPartnerViewModel
{
    [Required]
    public int PartnerId { get; set; }

    [Required]
    [StringLength(50)]
    [Display(Name = "Partner Code")]
    public string PartnerCode { get; set; }
        = string.Empty;

    [Required]
    [StringLength(200)]
    [Display(Name = "Partner Name")]
    public string PartnerName { get; set; }
        = string.Empty;

    [StringLength(500)]
    public string? Address { get; set; }

    [EmailAddress]
    [StringLength(200)]
    public string? Email { get; set; }

    [StringLength(50)]
    [Display(Name = "Phone Number")]
    public string? PhoneNumber { get; set; }

    [Display(Name = "Active Partner")]
    public bool IsActive { get; set; }
}