using AmlScreening.Application.Interface;
using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Models;
using AmlScreening.Application.Security;
using AmlScreening.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AmlScreening.Infrastructure.Repositories;

public sealed class ComplianceDashboardRepository(AmlScreeningDbContext db, ICurrentUserContext user) : IComplianceDashboardRepository
{
    public async Task<ComplianceDashboardSummary> GetSummaryAsync(int clientid, bool canViewTransactions,
        bool canViewAlerts, bool canViewCases, CancellationToken cancellationToken = default)
    {
        // The caller cannot broaden scope using clientid or these display booleans.
        // EF global owner filters restrict every count, including parent-linked alerts/cases.
        var result = new ComplianceDashboardSummary();
        if (user.HasPermission(AppPermissions.TransactionsView))
        {
            result.TransactionCount = await db.Transactions.CountAsync(cancellationToken);
            result.ReviewCount = await db.Transactions.CountAsync(x => x.Decision == "REVIEW", cancellationToken);
            result.BlockCount = await db.Transactions.CountAsync(x => x.Decision == "BLOCK", cancellationToken);
        }
        if (user.HasPermission(AppPermissions.AlertsView))
            result.OpenAlertCount = await db.ComplianceAlerts.CountAsync(x => x.Status != "CLOSED" && x.Status != "RESOLVED", cancellationToken);
        if (user.HasPermission(AppPermissions.CasesView))
            result.OpenCaseCount = await db.ComplianceCases.CountAsync(x => x.Status != "CLOSED", cancellationToken);
        return result;
    }
}
