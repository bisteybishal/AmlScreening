namespace AmlScreening.Domain.Entities;

public class CustomerKycScreeningLog
{
    public long Id { get; set; }


    public int PartnerId { get; set; }

    public int BranchId { get; set; }

    public long UserId { get; set; }

    public string Username { get; set; }
        = string.Empty;


    public string ScreeningType { get; set; }
        = string.Empty;

    // Customer details used for screening

    public string? FullName { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public string? Country { get; set; }

    public string? Gender { get; set; }

    public string? Occupation { get; set; }

    public string? SourceOfFunds { get; set; }

    // AML matching

    public string? MatchedName { get; set; }

    public int? AmlRecordId { get; set; }

    public decimal? NameMatchPercent { get; set; }
    public int MatchCount { get; set; }

    public decimal? HighestMatchPercent { get; set; }


    public decimal? RiskScore { get; set; }

    public string? RiskLevel { get; set; }


    public string Status { get; set; }
        = "PROCESSING";

    public string? ApprovedBy { get; set; }

    public DateTime? ApprovedDate { get; set; }

    public string? ApprovalRemarks { get; set; }

    public DateTime CreatedDate { get; set; }
    public string? CreatedBy { get; set; }

    public string? RejectedBy{ get; set; }
    public string? RejectedRemarks { get; set; }
    public DateTime? RejectedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public byte[] RowVersion { get; set; }
    = [];
}