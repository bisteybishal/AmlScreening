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


    public int MatchCount { get; set; }

    public decimal? HighestMatchPercent { get; set; }


    public decimal? RiskScore { get; set; }

    public string? RiskLevel { get; set; }


    public string Status { get; set; }
        = "PROCESSING";


    public DateTime CreatedDate { get; set; }

    public DateTime? CompletedDate { get; set; }
}