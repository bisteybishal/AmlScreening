using AmlScreening.Domain.Entities;
namespace AmlScreening.Application.Interface.Provider;
public interface IAmlImportRepository
{
    Task SaveAsync(IEnumerable<AmlRecord> records, CancellationToken cancellationToken = default);
}
