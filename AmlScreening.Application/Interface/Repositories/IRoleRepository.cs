using AmlScreening.Application.Models;

namespace AmlScreening.Application.Interface.Repositories;

public interface IRoleRepository
{
    Task<IReadOnlyList<RoleListItem>> GetAllAsync();
    Task<RoleListItem?> GetByIdAsync(int roleId);
    Task<IReadOnlyList<PermissionListItem>> GetPermissionsAsync(int roleId);
    Task<bool> RoleNameExistsAsync(string roleName);
    Task<int> CreateAsync(string roleName, bool isActive);
    Task<bool> UpdatePermissionsAsync(int roleId, IReadOnlyCollection<int> permissionIds);
}
