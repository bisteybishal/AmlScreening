using AmlScreening.Application.Interface;
using AmlScreening.Application.Models;
using AmlScreening.Infrastructure.Persistence;
using Dapper;
using Microsoft.Extensions.Logging;

namespace AmlScreening.Infrastructure.Services;

// Reads one table and builds the parent/child menu used by the shared layout.
public sealed class MenuService : IMenuService
{
    private readonly IDBConnectionFactory _connectionFactory;
    private readonly ICurrentUserContext _currentUser;

    public MenuService(
        IDBConnectionFactory connectionFactory,
        ICurrentUserContext currentUser)

    {
        _connectionFactory = connectionFactory;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<NavigationMenuItem>> GetForCurrentUserAsync(
        CancellationToken cancellationToken = default)
    {

        if (!_currentUser.IsAuthenticated)
        {
            return Array.Empty<NavigationMenuItem>();
        }
        const string sql = """
            SELECT MenuId,
                   ParentMenuId,
                   MenuKey,
                   DisplayName,
                   SectionName,
                   ControllerName,
                   ActionName,
                   IconClass,
                   PermissionCode,
                   SortOrder
            FROM dbo.MenuItems
            WHERE IsActive = 1
            ORDER BY  SortOrder, MenuId;
            """;


        using var connection = _connectionFactory.CreateConnection();
        var menus = await connection.QueryAsync<NavigationMenuItem>(sql);
        return menus.Where(item => string.IsNullOrWhiteSpace(item.PermissionCode) || _currentUser.HasPermission(item.PermissionCode)).ToList();
    }
}
