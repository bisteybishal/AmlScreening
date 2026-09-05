namespace AmlScreening.Application.Models;

public sealed class ComplianceDashboardSummary
{
    public int TransactionCount { get; set; }
    public int ReviewCount { get; set; }
    public int BlockCount { get; set; }
    public int OpenAlertCount { get; set; }
    public int OpenCaseCount { get; set; }
}
