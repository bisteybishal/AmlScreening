using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Domain.Entities.Compliance;
using AmlScreening.Application.Interface;
using AmlScreening.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace AmlScreening.Infrastructure.Repositories
{
    public class ComplianceRuleRepository:IComplianceRuleRepository
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
            return await _dbcontext.ComplianceRules.AsNoTracking().OrderBy(x => x.RuleType).ThenBy(x => x.RuleName).ToListAsync();
        }
        public async Task<ComplianceRule?> GetByIdAsync(int id)
        {
            return await _dbcontext.ComplianceRules.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        }
        public async Task<int> CreateAsync(ComplianceRule rule)
        {
            rule.ClientId = _currentUser.ClientId;
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
            rule.UpdatedDate = DateTime.UtcNow;
            rule.UpdatedBy = updatedBy;
            await using var transaction = await _dbcontext.Database.BeginTransactionAsync();
            try
            {
                _dbcontext.ComplianceRules.Update(rule);
                _dbcontext.Entry(rule).Property(x => x.RowVersion).OriginalValue = rule.RowVersion;
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
            var existingRule = await _dbcontext.ComplianceRules.FirstOrDefaultAsync(x => x.Id == id);
            if (existingRule == null)
            {
                return false;
            }
            existingRule.DeleteBy = deletedBy;
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
