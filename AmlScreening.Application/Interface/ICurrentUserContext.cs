namespace AmlScreening.Application.Interface;

public interface ICurrentUserContext
{
    bool IsAuthenticated { get; }
    bool IsAdmin { get; }

    long UserId { get; }

    int PartnerId { get; }

    int BranchId { get; }

    string Username { get; }

    bool HasPermission(string permissionCode);
}