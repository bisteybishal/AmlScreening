using AmlScreening.Application.Models;
using AmlScreening.Domain.Entities;

namespace AmlScreening.Application.Interface.Repositories;

public interface IUserRepository
{
    Task<User?> GetUserAsync(string username);
    Task<IReadOnlyList<UserListItem>> GetAllAsync();
    Task<UserListItem?> GetByIdAsync(long userId);
    Task<IReadOnlyList<RoleOption>> GetActiveRolesAsync();
    Task<IReadOnlyList<string>> GetPermissionsAsync(int userId);
    Task<IReadOnlyList<PermissionListItem>> GetPermissionOptionsAsync(long userId);
    Task<bool> UpdatePermissionsAsync(long userId, IReadOnlyCollection<int> permissionIds);
    Task<bool> UsernameExistsAsync(string username);
    Task<long> CreateAsync(User user, int roleId);
    Task<bool> UpdateRoleAsync(long userId, int roleId);
}
