using AmlScreening.Domain.Entities.Compliance;

namespace AmlScreening.Application.Interface.Repositories;

public interface IComplianceRuleRepository
{
    Task<IReadOnlyList<ComplianceRule>> GetAllAsync();
    Task<ComplianceRule?> GetByIdAsync(int id);
    Task<long> CreatePendingAsync(ComplianceRule rule, 
         long makeruserid, string makerusername);

    Task<long> CreatePendingVersionAsync(
        ComplianceRule currentApproved,
        ComplianceRule changedRule,
        long makerUserId,
        string makerUsername);
    Task<bool> UpdatePendingAsync(
        ComplianceRule rule, long currentUserId,
        string userName);

    Task<ComplianceRule?> GetPendingForReviewAsync(int id);

    Task<ComplianceRule?>
        GetActiveVersionAsync(
            string ruleCode,
            int clientId);

    Task<bool> ApproveAsync(
        int id,
        long checkerUserId,
        string checkerUsername,
        byte[] rowVersion);

    Task<bool> DeletePendingAsync(
        int id,
        long currentUserId,
        string deletedBy);
}