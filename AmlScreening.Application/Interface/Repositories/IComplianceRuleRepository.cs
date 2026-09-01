using AmlScreening.Domain.Entities.Compliance;

namespace AmlScreening.Application.Interface.Repositories;

public interface IComplianceRuleRepository
{
    Task<IReadOnlyList<ComplianceRule>> GetAllAsync();
    Task<ComplianceRule?> GetByIdAsync(int id);
    Task<int> CreateAsync(ComplianceRule rule);
    Task<bool> UpdateAsync(
        ComplianceRule rule,
        string updatedBy);

    Task<bool> DeleteAsync(
        int id,
        string deletedBy);
}