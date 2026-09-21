using System.ComponentModel.DataAnnotations;

namespace AmlScreening.Portal.ViewModels;

public class EditBranchViewModel
{
    [Required]
    public int BranchId { get; set; }
    public int PartnerId { get; set; }
    [Required]
    [StringLength(200)]
    [Display(Name = "Branch Name")]
    public string BranchName { get; set; } = string.Empty;
    [StringLength(500)]
    public string? Address { get; set; } 
    [EmailAddress]
    [StringLength(200)]
    public string? Email { get; set; } 
    [StringLength(50)]
    [Display(Name = "Phone Number")]
    public string? PhoneNumber { get; set; } 
    [Display(Name = "Active Branch")]
    public bool IsActive { get; set; }
}
