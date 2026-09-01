using AmlScreening.Application.Models;
using AmlScreening.Domain.Entities;

namespace AmlScreening.Portal.ViewModels;

public sealed class DashboardViewModel
{
    public string FullName { get; set; } = "User";
    public string Username { get; set; } = "User";
    public string RoleName { get; set; } = "User";
    public AmlRecordSummary Summary { get; set; } = new();
    public IReadOnlyList<AmlRecord> RecentRecords { get; set; } = Array.Empty<AmlRecord>();
    public int TransactionCount { get; set; }
    public int ReviewCount { get; set; }
    public int BlockCount { get; set; }
    public int OpenAlertCount { get; set; }
    public int OpenCaseCount { get; set; }
    public string? ErrorMessage { get; set; }
}
