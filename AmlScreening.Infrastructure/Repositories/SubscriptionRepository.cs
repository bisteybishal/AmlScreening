using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Domain.Entities;
using AmlScreening.Infrastructure.Persistence;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace AmlScreening.Infrastructure.Repositories
{
    public class SubscriptionRepository:ISubscriptionRepository
    {
        private readonly IDBConnectionFactory _connectionFactory;
        public SubscriptionRepository(IDBConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }
        public async Task<IEnumerable<SubscriptionPlan>> GetAllAsync()
        {
            using var connection = _connectionFactory.CreateConnection();
            var result=await connection.QueryAsync<SubscriptionPlan>("spa_AML_SubscriptionPlan_GetAll", commandType: CommandType.StoredProcedure);
            return result;
        }

        public async Task<SubscriptionPlan> GetByIdAsync(int id)
        {
            string sql = "select * from AML_Subscription_plan where planId=@id;";
            using var connection = _connectionFactory.CreateConnection();
            return await connection.QueryFirstOrDefault(sql, new { id = @id });
        }
        public async Task<int> CreateSubscriptionAsync(SubscriptionPlan plan)
        {
            const string subscription = """
                insert into AML_Subscription_plan (SourceCode,
                SourceName,
                DurationMonths,
                Rate,
                IsActive)OUTPUT INSERTED.PlanId
                values(@SourceCode,@SourceName,@DurationMonths,@rate,@isactive)
                """;
            using var connection =  _connectionFactory.CreateConnection();
            return await connection.ExecuteScalarAsync<int>(subscription, plan);

        }
        public async Task<bool>DeleteAsync(int Id)
        {
            string sql = "delete from AML_Subscription_plan where planId=@id;";
            using var connection = _connectionFactory.CreateConnection();
            return await connection.ExecuteAsync(sql, new { id = @Id }) == 0;
        }
    }
}
