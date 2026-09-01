using AmlScreening.Application.Models;

namespace AmlScreening.Application.Interface;

public interface IAuditService
{
    Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditLogItem>> GetRecentAsync(
        int maximumRecords = 200,
        CancellationToken cancellationToken = default);
}
