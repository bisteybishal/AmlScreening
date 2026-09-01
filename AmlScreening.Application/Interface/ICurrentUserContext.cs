namespace AmlScreening.Application.Interface;

public interface ICurrentUserContext
{
    bool IsAuthenticated { get; }
    long UserId { get; }
    int ClientId { get; }
    string Username { get; }
    bool HasPermission(string permissionCode);
}
