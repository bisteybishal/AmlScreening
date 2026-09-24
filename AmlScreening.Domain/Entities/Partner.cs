namespace AmlScreening.Domain.Entities;

public sealed class Partner
{
    public int PartnerId { get; set; }

    public string PartnerCode { get; set; }
        = string.Empty;

    public string PartnerName { get; set; }
        = string.Empty;

    public string? Address { get; set; }
    public string? Country { get; set; }

    public string? City { get; set; }

    public string? Email { get; set; }

    public string? PhoneNumber { get; set; }

    public bool IsActive { get; set; }
        = true;

    public DateTime CreatedDate { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public string? UpdatedBy { get; set; }
}
