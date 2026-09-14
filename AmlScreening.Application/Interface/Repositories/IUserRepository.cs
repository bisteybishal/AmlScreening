using AmlScreening.Application.Models;
using AmlScreening.Domain.Entities;

namespace AmlScreening.Application.Interface.Repositories;

public interface IUserRepository
{
    Task<User?> GetUserAsync(string username);
    Task<IReadOnlyList<RoleOption>> GetUserRolesAsync(long userId);
    Task<UserPageResult> GetPageAsync(int pageNumber, int pageSize);
    Task<UserListItem?> GetByIdAsync(long userId);
    Task<IReadOnlyList<RoleOption>> GetActiveRolesAsync();
    Task<IReadOnlyList<string>> GetPermissionsAsync(long userId);
    Task UpdateLastLoginAsync(long userId);
    Task<IReadOnlyList<PermissionListItem>> GetPermissionOptionsAsync(long userId);
    Task<bool> UpdatePermissionsAsync(long userId, IReadOnlyCollection<int> permissionIds);
    Task<bool> UsernameExistsAsync(string username);
    Task<long> CreateAsync(User user, IReadOnlyCollection<int> roleIds);
    Task<bool> UpdateRolesAsync(long userId, IReadOnlyCollection<int> roleIds);
    Task<IReadOnlyList<ClientOption>> GetActiveClientsAsync();

    Task<IReadOnlyList<PermissionListItem>> GetRolePermissionAsync(int roleid);
}
