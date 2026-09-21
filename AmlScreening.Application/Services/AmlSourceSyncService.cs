using AmlScreening.Application.Interface.Provider;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace AmlScreening.Application.Services
{
    public class AmlSourceSyncService : IAmlSourceSyncService
    {
        private readonly IEnumerable<IAmlSourceStrategy> _strategies;
        private readonly IAmlImportRepository _repository;

        public AmlSourceSyncService(IEnumerable<IAmlSourceStrategy> strategies, IAmlImportRepository repository)
        {
            _strategies = strategies;
            _repository = repository;
        }
        public async Task<int> SyncAsync(string sourceCode,CancellationToken cancellation=default)
        {
            var validSourcecode=sourceCode.Trim().ToUpperInvariant();
            var strategy = _strategies.FirstOrDefault(s => s.SourceCode.Equals(validSourcecode, StringComparison.OrdinalIgnoreCase));
            if (strategy == null)
            {
                throw new InvalidOperationException($"No strategy found for source code: {sourceCode}");
            }
            var records = await strategy.GetRecordsAsync();
            await _repository.SaveAsync(validSourcecode,records,cancellation);
            return records.Count;
        }
    }
}
