namespace AmlScreening.Application.Models;

public sealed class NavigationMenuItem
{
    public int MenuId { get; init; }
    public int? ParentMenuId { get; init; }
    public string MenuKey { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string SectionName { get; init; } = "MAIN";
    public string? ControllerName { get; init; }
    public string? ActionName { get; init; }
    public string IconClass { get; init; } = "bi-circle";
    public int SortOrder { get; init; }
    public string? PermissionCode { get; init; }
    public IReadOnlyList<NavigationMenuItem> Children { get; init; } = Array.Empty<NavigationMenuItem>();
}
