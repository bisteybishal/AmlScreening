namespace AmlScreening.Application.Models;

public sealed class AccountAccessInfo
{
    public long UserId { get; set; }
    public int ClientId { get; set; }
    public string Username { get; set; } = "";
    public string FullName { get; set; } = "";
    public bool IsActive { get; set; }
    public bool CanAccessPortal { get; set; }
}

public sealed class ApiApplicationInfo
{
    public int Id { get; set; }
    public string Issuer { get; set; } = "";
    public string Subject { get; set; } = "";
    public bool IsActive { get; set; }
}
