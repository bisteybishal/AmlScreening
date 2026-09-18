using AmlScreening.Application.Interface;
using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Security;
using AmlScreening.Domain.Entities.Compliance;
using AmlScreening.Infrastructure.Persistence;
using Dapper;
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
            using var connection = _dBConnectionFactory.CreateConnection();
            var rows = await connection.QueryAsync<ComplianceRule>(
               "dbo.spa_ComplianceRules_GetAll", new
               {
                   clientid = _currentUser.ClientId,
                   canviewAll = CanViewAllClients()
               }, commandType: CommandType.StoredProcedure);
            return rows.AsList();

        }
        public async Task<ComplianceRule?> GetByIdAsync(int id)
        {
            using var connection = _dBConnectionFactory.CreateConnection();
            var result = await connection.QueryFirstOrDefaultAsync<ComplianceRule>
                ("dbo.spa_ComplianceRules_GetById", new
                {
                    Id = id,
                    clientid = _currentUser.ClientId,
                },
                     commandType: CommandType.StoredProcedure);
            return result;



        }
        public async Task<long> CreatePendingAsync(ComplianceRule rule,
             long makerUserId, string makerUsername)
        {
            using var connection = _dBConnectionFactory.CreateConnection();
            var parameters = new
            {
                ClientId = _currentUser.ClientId,
                rule.RuleCode,
                rule.RuleName,
                rule.RuleType,
                rule.ThresholdAmount,
                rule.Currency,
                rule.LookbackMinutes,
                rule.TransactionCount,
                rule.RiskScore,
                rule.Action,
                rule.Priority,
                MakerUserId = makerUserId,
                MakerUsername = makerUsername,
               

            };
            return await connection.QuerySingleAsync<long>("dbo.spa_ComplianceRules_Create",
                parameters, commandType: CommandType.StoredProcedure);
        }
        public async Task<bool> UpdatePendingAsync(ComplianceRule rule,
        long currentUserId,
        string userName)
        {
            using var connection = _dBConnectionFactory.CreateConnection();
            var updateDetail = await connection.QuerySingleAsync<int>(
                "dbo.spa_ComplianceRules_UdpatePending", new
                {
                    rule.Id,
                    clientid = _currentUser.ClientId,
                    rule.RuleName,
                    rule.RuleType,
                    rule.ThresholdAmount,
                    rule.Currency,
                    rule.RiskScore,
                    rule.TransactionCount,
                    rule.LookbackMinutes,
                    rule.Action,
                    rule.Priority,
                    CurrentUserId =
                        currentUserId,

                    Username =
                        userName,

                    rule.RowVersion
                }, commandType: CommandType.StoredProcedure);
            return updateDetail > 0;
        }

        public async Task<ComplianceRule?>
        GetPendingForReviewAsync(
            int id)
        {
            using var connection =
                _dBConnectionFactory.CreateConnection();

            return await connection
                .QueryFirstOrDefaultAsync<ComplianceRule>(
                    "dbo.spa_ComplianceRules_GetPendingForReview",
                    new
                    {
                        Id = id,

                        ClientId =
                            _currentUser.ClientId
                    },
                    commandType:
                        CommandType.StoredProcedure);
        }
        public async Task<ComplianceRule?>
        GetActiveVersionAsync(
            string ruleCode,
            int clientId)
        {
            if (clientId != _currentUser.ClientId)
            {
                return null;
            }
            using var connection =
                _dBConnectionFactory.CreateConnection();

            return await connection
                .QueryFirstOrDefaultAsync<ComplianceRule>(
                    "dbo.spa_ComplianceRules_GetActiveVersion",
                    new
                    {
                        RuleCode = ruleCode,
                        ClientId = _currentUser.ClientId

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
            using var connection =
                _dBConnectionFactory.CreateConnection();

            var affected =
                await connection.QuerySingleAsync<int>(
                    "dbo.spa_ComplianceRules_Approve",
                    new
                    {
                        Id = id,

                        ClientId =
                            _currentUser.ClientId,

                        CheckerUserId =
                            checkerUserId,

                        CheckerUsername =
                            checkerUsername,

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
       string deletedBy)
        {

            using var connection =
                _dBConnectionFactory.CreateConnection();

            var affected =
                await connection.ExecuteScalarAsync<int>(
                    "dbo.spa_ComplianceRules_DeletePending",
                    new
                    {
                        Id = id,

                        ClientId =
                            _currentUser.ClientId,

                        CurrentUserId =
                            currentUserId,

                        DeletedBy =
                            deletedBy
                    },
                    commandType:
                        CommandType.StoredProcedure);

            return affected > 0;
        }
        public async Task<long>
        CreatePendingVersionAsync(
            ComplianceRule currentApproved,
            ComplianceRule changedRule,
            long makerUserId,
            string makerUsername)
        {
            using var connection =
                _dBConnectionFactory.CreateConnection();

            return await connection
                .QuerySingleAsync<long>(
                    "dbo.spa_ComplianceRules_CreatePendingVersion",
                    new
                    {
                        ClientId =
                            _currentUser.ClientId,

                        CurrentRuleId =
                            currentApproved.Id,

                        currentApproved.RuleCode,

                        changedRule.RuleName,
                        changedRule.RuleType,
                        changedRule.ThresholdAmount,
                        changedRule.Currency,
                        changedRule.LookbackMinutes,
                        changedRule.TransactionCount,
                        changedRule.RiskScore,
                        changedRule.Action,
                        changedRule.Priority,

                        MakerUserId =
                            makerUserId,

                        MakerUsername =
                            makerUsername
                    },
                    commandType:
                        CommandType.StoredProcedure);
        }
        private bool CanViewAllClients()
        {
            return
                _currentUser.HasPermission(
                    AppPermissions.PlatformCrossClientAccess);
              
        }

    }
}
