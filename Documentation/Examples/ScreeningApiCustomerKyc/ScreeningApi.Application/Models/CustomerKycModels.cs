namespace ScreeningApi.Application.Models;

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
public sealed record RiskConfiguration(List<Factor> Factors, List<Rule> Rules, List<Band> Bands);
public sealed class SavedKycResponse
{
    public byte[] RequestHash { get; set; } = [];
    public string ResponseJson { get; set; } = "";
}
