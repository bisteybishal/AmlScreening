using AmlScreening.Application.Models;

namespace AmlScreening.Application.Interface.Repositories;

public interface IComplianceDashboardRepository
{
    Task<ComplianceDashboardSummary> GetSummaryAsync(int clientid, bool canViewTransactions,
        bool canViewAlerts,
        bool canViewCases,
        CancellationToken cancellationToken = default);
}
