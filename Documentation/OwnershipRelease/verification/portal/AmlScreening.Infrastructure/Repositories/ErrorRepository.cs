using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Models;
using AmlScreening.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using System.Data;

namespace AmlScreening.Infrastructure.Repositories
{
    public class ErrorRepository : IErrorRepository
    {
        private readonly IDBConnectionFactory _dbConnectionFactory;

        public ErrorRepository(
            IDBConnectionFactory dbConnectionFactory)
        {
            _dbConnectionFactory = dbConnectionFactory;
        }

        public async Task<int> LogErrorAsync(
            ErrorLogModel error,
            CancellationToken cancellationToken = default)
        {
            using var connection =
                _dbConnectionFactory.CreateConnection();

            await connection.OpenAsync(cancellationToken);

            using var command =
                connection.CreateCommand();

            command.CommandText = "dbo.spa_SOAPError";

            command.CommandType =
                CommandType.StoredProcedure;

            command.Parameters.Add(
                new SqlParameter(
                    "@error_desc",
                    SqlDbType.VarChar,
                    2000)
                {
                    Value = Limit(
                        error.ErrorDescription,
                        2000)
                });

            command.Parameters.Add(
                new SqlParameter(
                    "@partnercode",
                    SqlDbType.VarChar,
                    200)
                {
                    Value = DbValue(
                        error.PartnerCode)
                });

            command.Parameters.Add(
                new SqlParameter(
                    "@ErrorNumber",
                    SqlDbType.VarChar,
                    50)
                {
                    Value = DbValue(
                        error.ErrorNumber)
                });

            command.Parameters.Add(
                new SqlParameter(
                    "@Script",
                    SqlDbType.VarChar,
                    1000)
                {
                    Value = Limit(
                        error.Script,
                        1000)
                });

            command.Parameters.Add(
                new SqlParameter(
                    "@ErrorScript",
                    SqlDbType.VarChar,
                    500)
                {
                    Value = Limit(
                        error.ErrorScript,
                        500)
                });

            command.Parameters.Add(
                new SqlParameter(
                    "@QueryString",
                    SqlDbType.VarChar,
                    3000)
                {
                    Value = DbValue(
                        Limit(
                            error.QueryString,
                            3000))
                });

            command.Parameters.Add(
                new SqlParameter(
                    "@ip",
                    SqlDbType.VarChar,
                    45)
                {
                    Value = DbValue(
                        Limit(
                            error.IpAddress,
                            45))
                });

            var result =
                await command.ExecuteScalarAsync(
                    cancellationToken);

            if (result == null ||
                result == DBNull.Value)
            {
                return 0;
            }

            return Convert.ToInt32(result);
        }

        private static object DbValue(
            string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? DBNull.Value
                : value;
        }

        private static string Limit(
            string? value,
            int maxLength)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Length <= maxLength
                ? value
                : value[..maxLength];
        }
    }
}