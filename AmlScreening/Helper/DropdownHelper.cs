using AmlScreening.Infrastructure.Data;
using Dapper;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace AmlScreening.Portal.Helper
{
    public class DropdownHelper
    {
        private readonly AmlScreeningDbContext _dbContext;

        public DropdownHelper(AmlScreeningDbContext dbContext)
        {
            _dbContext = dbContext;
        }
        public async Task<List<SelectListItem>>GetDropdownAsync(string factorCode,CancellationToken cancellation = default)
        {
            var command = new CommandDefinition("dbo.spa_KycDropdown_Get", new
            {
                Factorcode = factorCode
            }, commandType: CommandType.StoredProcedure,
            cancellationToken: cancellation);

            var conn =  _dbContext.Database.GetDbConnection();
            var items = await conn.QueryAsync <SelectListItem>(command);
            return items.ToList();

        }

        public async Task<List<SelectListItem>> GetNationalityListAsync()
        {
            var command = new CommandDefinition("dbo.spa_NationalityList_Get", commandType: CommandType.StoredProcedure);

            var conn = _dbContext.Database.GetDbConnection();
            var items = await conn.QueryAsync<SelectListItem>(command);
            return items.ToList();

        }

        public async Task<List<SelectListItem>> GetCountryListAsync(CancellationToken cancellationToken = default)
        {
            var command = new CommandDefinition("dbo.spa_CountryList_Get", commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken);
           

            var conn = _dbContext.Database.GetDbConnection();
            var items = await conn.QueryAsync<SelectListItem>(command);
            return items.ToList();

        }

        public async Task<List<SelectListItem>> GetAmlSourceNameAsync(CancellationToken cancellationToken = default)
        {
            var command = new CommandDefinition("dbo.spa_AmlSourceName_Get", commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken);


            var conn = _dbContext.Database.GetDbConnection();
            var items = await conn.QueryAsync<SelectListItem>(command);
            return items.ToList();

        }
    }
}
