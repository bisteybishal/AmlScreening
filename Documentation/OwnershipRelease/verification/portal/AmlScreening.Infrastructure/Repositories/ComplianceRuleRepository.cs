using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Domain.Entities.Compliance;
using AmlScreening.Application.Interface;
using AmlScreening.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AmlScreening.Infrastructure.Repositories
{
    public sealed class ComplianceRuleRepository : IComplianceRuleRepository
    {
        private readonly AmlScreeningDbContext _dbcontext;
        private readonly ICurrentUserContext _currentUser;
        public ComplianceRuleRepository(
            AmlScreeningDbContext dbContext,
            ICurrentUserContext currentUser)
        {
            _dbcontext = dbContext;
            _currentUser = currentUser;

        }
        public async Task<IReadOnlyList<ComplianceRule>> GetAllAsync()
        {
            EnsureSignedIn();
            return await _dbcontext.ComplianceRules.AsNoTracking().OrderBy(x => x.RuleType).ThenBy(x => x.RuleName).ToListAsync();
        }
        public async Task<ComplianceRule?> GetByIdAsync(int id)
        {
            EnsureSignedIn();
            return await _dbcontext.ComplianceRules.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        }
        public async Task<int> CreateAsync(ComplianceRule rule)
        {
            EnsureSignedIn();
            if (rule.Id != 0)
                throw new ArgumentException("A new rule must not have an existing ID.", nameof(rule));
            rule.ClientId = _currentUser.ClientId;
            rule.CreatedDate = DateTime.UtcNow;
            rule.CreatedBy = _currentUser.Username;
            rule.UpdatedDate = null;
            rule.UpdatedBy = null;
            rule.DeleteFg = "N";
            rule.DeleteDate = null;
            rule.DeleteBy = null;
            await using var transaction = await _dbcontext.Database.BeginTransactionAsync();
            try
            {
                _dbcontext.ComplianceRules.Add(rule);
                await _dbcontext.SaveChangesAsync();
                AddAudit("Create", rule.Id, $"Created compliance rule {rule.RuleCode}.");
                await _dbcontext.SaveChangesAsync();
                await transaction.CommitAsync();
                return rule.Id;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        public async Task<bool> UpdateAsync(ComplianceRule rule, string updatedBy)
        {
            EnsureSignedIn();
            // The context's client filter applies when loading the stored row.
            var existing = await _dbcontext.ComplianceRules.FirstOrDefaultAsync(x => x.Id == rule.Id);
            if (existing == null || rule.RowVersion is not { Length: 8 })
                return false;

            existing.RuleCode = rule.RuleCode;
            existing.RuleName = rule.RuleName;
            existing.RuleType = rule.RuleType;
            existing.ThresholdAmount = rule.ThresholdAmount;
            existing.Currency = rule.Currency;
            existing.LookbackMinutes = rule.LookbackMinutes;
            existing.TransactionCount = rule.TransactionCount;
            existing.RiskScore = rule.RiskScore;
            existing.Action = rule.Action;
            existing.RuleVersion = rule.RuleVersion;
            existing.Priority = rule.Priority;
            existing.ApprovalStatus = rule.ApprovalStatus;
            existing.EffectiveFrom = rule.EffectiveFrom;
            existing.EffectiveTo = rule.EffectiveTo;
            existing.IsActive = rule.IsActive;
            existing.UpdatedDate = DateTime.UtcNow;
            existing.UpdatedBy = _currentUser.Username;
            await using var transaction = await _dbcontext.Database.BeginTransactionAsync();
            try
            {
                _dbcontext.Entry(existing).Property(x => x.RowVersion).OriginalValue = rule.RowVersion;
                var updated = await _dbcontext.SaveChangesAsync() > 0;
                if (!updated)
                {
                    await transaction.RollbackAsync();
                    return false;
                }

                AddAudit("Update", rule.Id, $"Updated compliance rule {rule.RuleCode}.");
                await _dbcontext.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                return false;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        public async Task<bool> DeleteAsync(int id, string deletedBy)
        {
            EnsureSignedIn();
            var existingRule = await _dbcontext.ComplianceRules.FirstOrDefaultAsync(x => x.Id == id);
            if (existingRule == null)
            {
                return false;
            }
            existingRule.DeleteBy = _currentUser.Username;
            existingRule.DeleteDate = DateTime.UtcNow;
            existingRule.DeleteFg = "Y";
            existingRule.IsActive = false;
            await using var transaction = await _dbcontext.Database.BeginTransactionAsync();
            try
            {
                var deleted = await _dbcontext.SaveChangesAsync() > 0;
                if (!deleted)
                {
                    await transaction.RollbackAsync();
                    return false;
                }

                AddAudit("Delete", existingRule.Id, $"Soft deleted compliance rule {existingRule.RuleCode}.");
                await _dbcontext.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
           
        }

        private void EnsureSignedIn()
        {
            if (!_currentUser.IsAuthenticated || _currentUser.ClientId <= 0 || _currentUser.UserId <= 0)
                throw new UnauthorizedAccessException("A signed-in user with a client is required.");
        }

        private void AddAudit(string action, int entityId, string details)
        {
            _dbcontext.AuditLogs.Add(new AmlScreening.Domain.Entities.AuditLog
            {
                ClientId = _currentUser.ClientId,
                UserId = _currentUser.UserId,
                Username = _currentUser.Username,
                Action = action,
                EntityType = "ComplianceRule",
                EntityId = entityId.ToString(),
                Details = details,
                CreatedDate = DateTime.UtcNow
            });
        }

    }
}
