using Dapper;
using ScreeningApi.Application.Interface.Repositories;
using ScreeningApi.Application.Models;
using ScreeningApi.Infrastructure.Persistence;
using System.Data;

namespace ScreeningApi.Infrastructure.Repositories
{
    public class CustomerScreeningRepository : ICustomerScreeningRepository
    {
        private readonly IDBConnectionFactory _dBConnectionFactory;

        public CustomerScreeningRepository(IDBConnectionFactory dBConnectionFactory)
        {
            _dBConnectionFactory = dBConnectionFactory;
        }

        public async Task<CustomerScreeningDbResult> ScreenAsync(string fullname , CancellationToken cancellationToken = default)
        {
            try
            {
                using var connection = _dBConnectionFactory.CreateConnection();

                var command = new CommandDefinition(commandText: "spa_CustomerScreening_Check", parameters: new
                {

                    FullName = fullname.Trim(),

                },
                commandType: CommandType.StoredProcedure, commandTimeout: 60, cancellationToken: cancellationToken);
                return await connection.QuerySingleAsync<CustomerScreeningDbResult>(command);

            }
            catch (Exception ex)
            {
                // Log the exception or handle it as needed
                throw new Exception("An error occurred while screening the customer.", ex);
            }
            
                       
        }
    }

   
}

