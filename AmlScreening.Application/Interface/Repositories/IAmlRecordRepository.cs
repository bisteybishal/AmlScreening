using AmlScreening.Domain.Entities;

namespace AmlScreening.Application.Interface.Repositories;

public interface IAmlRecordRepository
{
    Task<IReadOnlyList<AmlRecord>> GetAllAsync();
    Task<AmlRecord?> GetByIdAsync(int id);
    Task<int> CreateAsync(AmlRecord record);
    Task<bool> UpdateAsync(AmlRecord record);
    Task<bool> DeleteAsync(int id);
}
