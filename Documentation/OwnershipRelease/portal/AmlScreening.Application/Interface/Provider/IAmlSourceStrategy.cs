using AmlScreening.Domain.Entities;
namespace AmlScreening.Application.Interface.Provider;
public interface IAmlSourceStrategy
{
    string SourceCode { get; }
    Task<List<AmlRecord>> GetRecordsAsync(CancellationToken cancellationToken = default);
}
