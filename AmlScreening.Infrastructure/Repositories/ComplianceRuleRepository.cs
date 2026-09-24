using AmlScreening.Application.Interface;
using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Security;
using AmlScreening.Domain.Entities.Compliance;
using AmlScreening.Infrastructure.Persistence;
using Dapper;
using Microsoft.VisualBasic;
using System.Data;

namespace AmlScreening.Infrastructure.Repositories
{
    public sealed class ComplianceRuleRepository : IComplianceRuleRepository
    {
        private readonly ICurrentUserContext _currentUser;
        private readonly IDBConnectionFactory _dBConnectionFactory;

        public ComplianceRuleRepository(ICurrentUserContext currentUser, IDBConnectionFactory dBConnectionFactory)
        {
            _currentUser = currentUser;
            _dBConnectionFactory = dBConnectionFactory;
        }

        public async Task<IReadOnlyList<ComplianceRule>> GetAllAsync()
        {
            EnsurePartnerContext();
            using var connection = _dBConnectionFactory.CreateConnection();
            var rows = await connection.QueryAsync<ComplianceRule>(
               "dbo.spa_ComplianceRules_GetAll", new
               {
                   PartnerId = _currentUser.PartnerId,
               }, commandType: CommandType.StoredProcedure);
            return rows.AsList();

        }
        public async Task<ComplianceRule?> GetByIdAsync(int id)
        {
            EnsurePartnerContext();
            using var connection = _dBConnectionFactory.CreateConnection();
            var command =
        new CommandDefinition(
            "dbo.spa_ComplianceRules_GetById",
            new
            {
                Id =
                    id,

                PartnerId =
                    _currentUser.PartnerId
            },
            commandType:
                CommandType.StoredProcedure);


            return await connection
                .QueryFirstOrDefaultAsync<ComplianceRule>(
                    command);



        }
        public async Task<long> CreatePendingAsync(ComplianceRule rule,
             long makerUserId, string makerUsername)
        {
            EnsurePartnerContext();

            EnsureCurrentUser(
                makerUserId);

            using var connection = _dBConnectionFactory.CreateConnection();
            var parameters = new
            {
                PartnerId = _currentUser.PartnerId,
                RuleCode = NormalizeRequired(rule.RuleCode),
                RuleName = NormalizeRequired(rule.RuleName),
                RuleType = NormalizeRequired(rule.RuleType),
                rule.ThresholdAmount,
                rule.Currency,
                rule.LookbackMinutes,
                rule.TransactionCount,
                rule.RiskScore,
                Action = NormalizeRequired(rule.Action).ToUpperInvariant(),
                rule.Priority,
                MakerUserId = makerUserId,
                MakerUsername = NormalizeRequired(makerUsername),


            };
            return await connection.QuerySingleAsync<long>("dbo.spa_ComplianceRules_Create",
                parameters, commandType: CommandType.StoredProcedure);
        }
        public async Task<bool> UpdatePendingAsync(ComplianceRule rule,
        long currentUserId,
        string userName)
        {
            EnsurePartnerContext();
            EnsureCurrentUser(currentUserId);
            using var connection = _dBConnectionFactory.CreateConnection();
            var updateDetail = await connection.QuerySingleAsync<int>(
                "dbo.spa_ComplianceRules_UpdatePending", new
                {
                    rule.Id,
                    PartnerId = _currentUser.PartnerId,
                    RuleName =
                            NormalizeRequired(
                                rule.RuleName),

                    RuleType =
                            NormalizeRequired(
                                rule.RuleType),
                    rule.ThresholdAmount,
                    Currency =
                            NullIfWhiteSpace(
                                rule.Currency)?
                                .ToUpperInvariant(),
                    rule.RiskScore,
                    rule.TransactionCount,
                    rule.LookbackMinutes,
                    Action =
                            NormalizeRequired(
                                rule.Action)
                                .ToUpperInvariant(),
                    rule.Priority,
                    CurrentUserId =
                        currentUserId,

                    Username =
                            NormalizeRequired(
                                userName),

                    rule.RowVersion
                }, commandType: CommandType.StoredProcedure);
            return updateDetail > 0;
        }
        public async Task<long>
      CreatePendingVersionAsync(
          int approvedRuleId,
          ComplianceRule changedRule,
          long makerUserId,
          string makerUsername)
        {
            EnsurePartnerContext();

            EnsureCurrentUser(
                makerUserId);
            using var connection =
                _dBConnectionFactory.CreateConnection();

            var UpdateId = await connection
                .QuerySingleAsync<long>(
                    "dbo.spa_ComplianceRules_CreatePendingVersion",
                    new
                    {
                        PartnerId = _currentUser.PartnerId,

                        ApprovedRuleId =
                            approvedRuleId,

                        RuleName =
                            NormalizeRequired(
                                changedRule.RuleName),

                        RuleType =
                            NormalizeRequired(
                                changedRule.RuleType),

                        changedRule.ThresholdAmount,

                        Currency =
                            NullIfWhiteSpace(
                                changedRule.Currency)?
                                .ToUpperInvariant(),
                        changedRule.LookbackMinutes,
                        changedRule.TransactionCount,
                        changedRule.RiskScore,
                        Action =
                            NormalizeRequired(
                                changedRule.Action)
                                .ToUpperInvariant(),
                        changedRule.Priority,

                        MakerUserId =
                            makerUserId,

                        MakerUsername =
                            NormalizeRequired(
                                makerUsername)
                    },
                    commandType:
                        CommandType.StoredProcedure);

            return UpdateId;
        }



        public async Task<ComplianceRule?>
        GetPendingForReviewAsync(
            int id)
        {
            EnsurePartnerContext();
            using var connection =
                _dBConnectionFactory.CreateConnection();

            return await connection
                .QueryFirstOrDefaultAsync<ComplianceRule>(
                    "dbo.spa_ComplianceRules_GetPendingForReview",
                    new
                    {
                        Id = id,

                        PartnerId =
                            _currentUser.PartnerId
                    },
                    commandType:
                        CommandType.StoredProcedure);
        }
        public async Task<ComplianceRule?>
        GetActiveVersionAsync(
            string ruleCode)
        {
            EnsurePartnerContext();
            using var connection =
                _dBConnectionFactory.CreateConnection();

            return await connection
                .QueryFirstOrDefaultAsync<ComplianceRule>(
                    "dbo.spa_ComplianceRules_GetActiveVersion",
                    new
                    {
                        RuleCode = ruleCode,
                        PartnerId = _currentUser.PartnerId

                    },
                    commandType:
                        CommandType.StoredProcedure);
        }

        public async Task<bool> ApproveAsync(
       int id,
       long checkerUserId,
       string checkerUsername,
       byte[] rowVersion)
        {
            EnsurePartnerContext();

            EnsureCurrentUser(
                checkerUserId);
            using var connection =
                _dBConnectionFactory.CreateConnection();

            var affected =
                await connection.QuerySingleAsync<int>(
                    "dbo.spa_ComplianceRules_Approve",
                    new
                    {
                        Id =
                            id,

                        PartnerId =
                            _currentUser.PartnerId,

                        CheckerUserId =
                            checkerUserId,

                        CheckerUsername =
                            NormalizeRequired(
                                checkerUsername),

                        RowVersion =
                            rowVersion
                    },
                    commandType:
                        CommandType.StoredProcedure);

            return affected > 0;
        }
        public async Task<bool> DeletePendingAsync(
       int id,
       long currentUserId,
       string deletedBy, byte[] rowVersion)

        {
            EnsurePartnerContext();

            EnsureCurrentUser(
                currentUserId);
            using var connection =
                _dBConnectionFactory.CreateConnection();

            var affected =
                await connection.QuerySingleAsync<int>(
                    "dbo.spa_ComplianceRules_DeletePending",
                    new
                    {
                        Id =
                            id,

                        PartnerId =
                            _currentUser.PartnerId,

                        CurrentUserId =
                            currentUserId,

                        DeletedBy =
                            NormalizeRequired(
                                deletedBy),

                        RowVersion =
                            rowVersion
                    },
                    commandType:
                        CommandType.StoredProcedure);

            return affected > 0;
        }
        private void EnsurePartnerContext()
        {
            if (!_currentUser.IsAuthenticated)
            {
                throw new UnauthorizedAccessException(
                    "Login required.");
            }


            if (_currentUser.UserId <= 0)
            {
                throw new UnauthorizedAccessException(
                    "Current user is invalid.");
            }


            if (_currentUser.PartnerId <= 0)
            {
                throw new UnauthorizedAccessException(
                    "Current partner is invalid.");
            }
        }
        private void EnsureCurrentUser(
        long suppliedUserId)
        {
            if (suppliedUserId <= 0
                ||
                suppliedUserId !=
                    _currentUser.UserId)
            {
                throw new UnauthorizedAccessException(
                    "The supplied user does not match the authenticated user.");
            }
        }
        private static string NormalizeRequired(
        string? value)
        {
            if (string.IsNullOrWhiteSpace(
                    value))
            {
                throw new ArgumentException(
                    "A required value is missing.");
            }


            return value.Trim();
        }
        private static string? NullIfWhiteSpace(
       string? value)
        {
            return string.IsNullOrWhiteSpace(
                    value)

                ? null

                : value.Trim();
        }
    }
}

