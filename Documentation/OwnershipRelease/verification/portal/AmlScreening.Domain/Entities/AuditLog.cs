namespace AmlScreening.Domain.Entities;

public sealed class AuditLog
{
    public long AuditLogId { get; set; }
    public int ClientId { get; set; }
    public long UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? Details { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
