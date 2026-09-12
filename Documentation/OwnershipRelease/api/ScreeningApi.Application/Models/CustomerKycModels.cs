using System.ComponentModel.DataAnnotations;

namespace ScreeningApi.Application.Models;

public sealed class CustomerKycRequest : IValidatableObject
{
    [StringLength(100)]
    public string? ClientReference { get; set; }
    [StringLength(100)]
    public string CustomerReference { get; set; } = string.Empty;
    [Required]
    public CustomerDetails Customer { get; set; } = new();

    public void NormalizeReference()
    {
        var supplied = ClientReference?.Trim();
        var legacy = CustomerReference?.Trim();
        if (string.IsNullOrWhiteSpace(supplied) && string.IsNullOrWhiteSpace(legacy))
            throw new ArgumentException("Client reference is required.");
        if (!string.IsNullOrWhiteSpace(supplied) && !string.IsNullOrWhiteSpace(legacy) &&
            !string.Equals(supplied, legacy, StringComparison.Ordinal))
            throw new ArgumentException("ClientReference and CustomerReference must agree.");
        CustomerReference = string.IsNullOrWhiteSpace(supplied) ? legacy! : supplied;
        if (CustomerReference.Length > 100) throw new ArgumentException("Client reference is too long.");
    }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        string? error = null;
        try { NormalizeReference(); } catch (ArgumentException ex) { error = ex.Message; }
        if (error != null) yield return new ValidationResult(error, new[] { nameof(ClientReference) });
    }
}

public sealed class CustomerKycResponse
{
    public long ResultId { get; set; }
    public int CustomerId { get; set; }
    public string CustomerReference { get; set; } = string.Empty;
    public int RiskScore { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public string Decision { get; set; } = string.Empty;
    public int RiskBandId { get; set; }
    public int BandVersion { get; set; }
    public DateTime CalculatedAtUtc { get; set; }
    public List<KycFactorResult> Factors { get; set; } = [];
    public CustomerScreeningResponse AmlResult { get; set; } = new();
}

public sealed record KycFactorResult(string FactorCode, string InputValue, int RuleId,
    int RuleVersion, int RawScore, decimal WeightPercent, decimal WeightedScore);

public sealed class KycRiskRule
{
    public int FactorId { get; set; }
    public string FactorCode { get; set; } = string.Empty;
    public decimal WeightPercent { get; set; }
    public int? RuleId { get; set; }
    public int RuleVersion { get; set; }
    public string? ValueCode { get; set; }
    public int Score { get; set; }
}

public sealed class KycRiskBand
{
    public int Id { get; set; }
    public int BandVersion { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public int MinimumScore { get; set; }
    public int? MaximumScore { get; set; }
    public string Decision { get; set; } = string.Empty;
}

public sealed class KycConfigurationException(string message) : Exception(message);
public sealed class KycClientException(string message) : Exception(message);


public sealed class KycCustomerException(string message) : Exception(message);
