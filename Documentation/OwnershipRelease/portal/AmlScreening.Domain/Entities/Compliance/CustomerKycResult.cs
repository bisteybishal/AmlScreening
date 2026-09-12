namespace AmlScreening.Domain.Entities.Compliance;

// Immutable screening history written by the API. Ownership is never taken from a form.
public sealed class CustomerKycResult
{
    public long Id { get; set; }
    public int ClientId { get; set; }
    public long? OwnerUserId { get; set; }
    public int? ApiApplicationId { get; set; }
    public string SubmissionChannel { get; set; } = "API";
    public int CustomerId { get; set; }
    public string CustomerReference { get; set; } = string.Empty;
    public int RiskScore { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public string Decision { get; set; } = string.Empty;
    public int RiskBandId { get; set; }
    public int BandVersion { get; set; }
    public DateTime CalculatedAtUtc { get; set; }
    public string FactorsJson { get; set; } = "[]";
    public string? RequestJson { get; set; }
    public string AmlResultJson { get; set; } = "{}";
    public string CorrelationId { get; set; } = string.Empty;
}
