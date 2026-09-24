using System.ComponentModel.DataAnnotations;

namespace AmlScreening.Portal.ViewModels;

public sealed class EditBranchViewModel
{
    [Range(1, int.MaxValue)]
    public int BranchId { get; set; }

    [Range(1, int.MaxValue)]
    public int PartnerId { get; set; }

    [Required]
    [StringLength(150)]
    [Display(Name = "Branch name")]
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