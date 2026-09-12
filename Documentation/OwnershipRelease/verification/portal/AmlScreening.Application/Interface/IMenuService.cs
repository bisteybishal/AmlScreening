using AmlScreening.Application.Models;

namespace AmlScreening.Application.Interface;

public interface IMenuService
{
    Task<IReadOnlyList<NavigationMenuItem>> GetForCurrentUserAsync(
        CancellationToken cancellationToken = default);
}
