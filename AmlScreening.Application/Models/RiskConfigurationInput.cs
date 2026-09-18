namespace AmlScreening.Application.Models;

// Only editable fields cross from the portal into the configuration service.
public sealed class RiskRuleInput
{
    public int Id { get; set; }
    public string FactorCode { get; set; } = "";
    public string? ValueCode { get; set; }
    public string? ValueName { get; set; }
    public decimal? MinimumValue { get; set; }
    public decimal? MaximumValue { get; set; }
    public int Score { get; set; }
    public string? Remarks { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class RiskBandInput
{
    public int Id { get; set; }
    public string RiskLevel { get; set; } = "";
    public int MinimumScore { get; set; }
    public int? MaximumScore { get; set; }
    public string Decision { get; set; } = "REVIEW";
    public string? Remarks { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
public sealed class RiskModelInput
{
    public int Id { get; set; }

    public decimal CustomerWeightPercent { get; set; }

    public decimal TransactionWeightPercent { get; set; }

    public decimal ScreeningWeightPercent { get; set; }

    public byte[] RowVersion { get; set; }
        = Array.Empty<byte>();
}
public sealed class RiskConfigurationException(string message) : Exception(message);
