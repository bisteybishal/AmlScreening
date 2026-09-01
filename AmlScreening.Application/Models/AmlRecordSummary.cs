namespace AmlScreening.Application.Models;

public sealed class AmlRecordSummary
{
    public int TotalRecords { get; init; }
    public int ActiveRecords { get; init; }
    public int IndividualRecords { get; init; }
    public int EntityRecords { get; init; }
    public int CountriesCovered { get; init; }
}
