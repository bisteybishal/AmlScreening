using AmlScreening.Application.Interface.Provider;
using AmlScreening.Application.Interface.Repositories;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmlScreening.Application.Services
{
    public class UnSyncService
    {
        private readonly IEnumerable<IAmlDataProvider> _providers;
        private readonly IAmlImportRepository _amlImportRepository;

        public UnSyncService(IEnumerable<IAmlDataProvider> providers, IAmlImportRepository amlImportRepository)
        {
            _providers = providers;
            _amlImportRepository = amlImportRepository;
        }
        public async Task<int> SyncAsync()
        {
            var provider = _providers.FirstOrDefault(x => x.sourcecode == "UN");
            if (provider == null)
            {
                throw new InvalidOperationException(
                    "UN data provider is not registered.");
            }
            var records = await provider.GetRecordsAsync();
            await _amlImportRepository.SaveAsync(records);
            return records.Count;
        }
    }
}
