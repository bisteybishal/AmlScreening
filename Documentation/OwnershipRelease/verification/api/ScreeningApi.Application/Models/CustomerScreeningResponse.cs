namespace ScreeningApi.Application.Models;

public sealed class CustomerScreeningResponse
{
    public string ClientReference { get; set; } = string.Empty;

    public string ScreeningStatus { get; set; } = string.Empty;

    public bool IsUnMatch { get; set; }

    public bool IsEuMatch { get; set; }

    // In this first version, this means a potential PEP name match.
    public bool IsPep { get; set; }

    public int HighestMatchScore { get; set; }

    public int? CustomerRiskScore { get; set; }

    public string? RiskLevel { get; set; }

    public string Decision { get; set; } = string.Empty;

    public List<string> Reasons { get; set; } = [];

    public string CorrelationId { get; set; } = string.Empty;
}