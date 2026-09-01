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
    private readonly ILogger<MenuService> _logger;

    public MenuService(
        IDBConnectionFactory connectionFactory,
        ICurrentUserContext currentUser,
        ILogger<MenuService> logger)
    {
        _connectionFactory = connectionFactory;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<IReadOnlyList<NavigationMenuItem>> GetForCurrentUserAsync(
        CancellationToken cancellationToken = default)
    {
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
            ORDER BY SectionName, SortOrder, MenuId;
            """;

        try
        {
            using var connection = _connectionFactory.CreateConnection();
            var rows = (await connection.QueryAsync<NavigationMenuItem>(
                new CommandDefinition(sql, cancellationToken: cancellationToken)))
                .Where(CanView)
                .ToList();

            var children = rows
                .Where(item => item.ParentMenuId.HasValue)
                .GroupBy(item => item.ParentMenuId!.Value)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<NavigationMenuItem>)group
                        .OrderBy(item => item.SortOrder)
                        .ToList());

            return rows
                .Where(item => !item.ParentMenuId.HasValue)
                .Where(item =>
                    !rows.Any(child => child.ParentMenuId == item.MenuId) ||
                    children.ContainsKey(item.MenuId))
                .OrderBy(item => item.SectionName)
                .ThenBy(item => item.SortOrder)
                .Select(item => CopyWithChildren(
                    item,
                    children.GetValueOrDefault(item.MenuId) ?? Array.Empty<NavigationMenuItem>()))
                .ToList();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,
                "Unable to load MenuItems. Run Database/MenuItems.sql against the AML database.");
            return Array.Empty<NavigationMenuItem>();
        }
    }

    private bool CanView(NavigationMenuItem item) =>
        string.IsNullOrWhiteSpace(item.PermissionCode) ||
        _currentUser.HasPermission(item.PermissionCode);

    private static NavigationMenuItem CopyWithChildren(
        NavigationMenuItem item,
        IReadOnlyList<NavigationMenuItem> children) => new()
    {
        MenuId = item.MenuId,
        ParentMenuId = item.ParentMenuId,
        MenuKey = item.MenuKey,
        DisplayName = item.DisplayName,
        SectionName = item.SectionName,
        ControllerName = item.ControllerName,
        ActionName = item.ActionName,
        IconClass = item.IconClass,
        PermissionCode = item.PermissionCode,
        SortOrder = item.SortOrder,
        Children = children
    };
}
