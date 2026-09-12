namespace AmlScreening.Application.Interface;

public interface ICurrentUserContext
{
    bool IsAuthenticated { get; }
    // Non-portal callers must explicitly supply administrator identity.
    bool IsAdministrator => false;
    long UserId { get; }
    int ClientId { get; }
    string Username { get; }
    bool HasPermission(string permissionCode);
}
