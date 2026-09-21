namespace AmlScreening.Application.Models;

public sealed class PartnerListItem
{
    public int PartnerId { get; set; }

    public string PartnerCode { get; set; }
        = string.Empty;

    public string PartnerName { get; set; }
        = string.Empty;


    public string Address { get; set; }
        = string.Empty;


    public string PhoneNumber { get; set; }
        = string.Empty;

    public string Country { get; set; }
    = string.Empty;

    public string Email { get; set; }
= string.Empty;


    public bool IsActive { get; set; }

    public int BranchCount { get; set; }

    public int UserCount { get; set; }

    public DateTime CreatedDate { get; set; }
}