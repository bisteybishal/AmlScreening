using AmlScreening.Application.Models;

namespace AmlScreening.Application.Interface.Repositories;

public interface IRoleRepository
{
    Task<IReadOnlyList<RoleListItem>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<RoleListItem?> GetByIdAsync(int roleId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PermissionListItem>> GetPermissionsAsync(int roleId, CancellationToken cancellationToken = default);
    Task<bool> RoleNameExistsAsync(string roleName, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(string roleName, bool isActive, CancellationToken cancellationToken = default);
    Task UpdatePermissionsAsync(int roleId, IReadOnlyCollection<int> permissionIds, CancellationToken cancellationToken = default);
}
