namespace AmlScreening.Domain.Entities;


public sealed class PartnerBranch
{
    public int BranchId { get; set; }

    public int PartnerId { get; set; }

    public string BranchName { get; set; }
        = string.Empty;

    public string? Address { get; set; }

    public string? Email { get; set; }

    public string? PhoneNumber { get; set; }

    public bool IsActive { get; set; }
        = true;

    public DateTime CreatedDate { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public string? UpdatedBy { get; set; }
}
