namespace AmlScreening.Application.Models;

public sealed record AuditEvent(
    string Action,
    string EntityType,
    string? EntityId = null,
    string? Details = null);

public sealed class AuditLogItem
{
    public long AuditLogId { get; init; }
    public int ClientId { get; init; }
    public long UserId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty;
    public string? EntityId { get; init; }
    public string? Details { get; init; }
    public DateTime CreatedDate { get; init; }
}
