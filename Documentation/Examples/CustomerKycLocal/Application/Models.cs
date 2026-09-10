using System.ComponentModel.DataAnnotations;

namespace CustomerKyc.Application;

public sealed class ScreeningRequest
{
    [Required, StringLength(100)] public string ClientReference { get; set; } = "";
    // Deliberately null unless supplied, so omitting "customer" fails validation.
    [Required] public CustomerInput Customer { get; set; } = null!;
}
public sealed class CustomerInput
{
    [Required, StringLength(200, MinimumLength = 2)] public string FullName { get; set; } = "";
    public DateOnly? DateOfBirth { get; set; }
    [RegularExpression("^[A-Z]{3}$")] public string? Nationality { get; set; }
    [RegularExpression("^[A-Z]{3}$")] public string? Country { get; set; }
    [StringLength(150)] public string? Occupation { get; set; }
    [StringLength(150)] public string? SourceOfFunds { get; set; }
    [StringLength(50)] public string? IdentificationType { get; set; }
    [StringLength(100)] public string? IdentificationNumber { get; set; }
}
public sealed class ScreeningResponse
{
    public string ClientReference { get; set; } = "";
    public string ScreeningStatus { get; set; } = "";
    public bool IsUnMatch { get; set; }
    public bool IsEuMatch { get; set; }
    public bool IsPep { get; set; }
    public int HighestMatchScore { get; set; }
    public int? CustomerRiskScore { get; set; }
    public string? RiskLevel { get; set; }
    public string Decision { get; set; } = "REVIEW";
    public List<string> Reasons { get; set; } = [];
    public string CorrelationId { get; set; } = "";
}
public sealed class Factor
{
    public int Id { get; set; }
    public string FactorCode { get; set; } = "";
    public decimal WeightPercent { get; set; }
}
public sealed class Rule
{
    public int Id { get; set; }
    public int RiskFactorId { get; set; }
    public string? ValueCode { get; set; }
    public int Score { get; set; }
    public int RuleVersion { get; set; }
}
public sealed class Band
{
    public int Id { get; set; }
    public int BandVersion { get; set; }
    public string RiskLevel { get; set; } = "";
    public int MinimumScore { get; set; }
    public int? MaximumScore { get; set; }
    public string Decision { get; set; } = "";
}
public sealed class NameMatch
{
    public int Id { get; set; }
    public string SourceName { get; set; } = "";
    public string ReferenceNo { get; set; } = "";
}
public sealed record FactorDetail(string FactorCode, string InputValue, int? RuleId,
    int? RuleVersion, int? RawScore, decimal WeightPercent, decimal? WeightedScore);
public sealed record RiskResult(int? Score, string? Level, string Decision,
    int? BandId, int? BandVersion, List<FactorDetail> Factors, List<string> Reasons);
public sealed class RequestConflictException : Exception;
