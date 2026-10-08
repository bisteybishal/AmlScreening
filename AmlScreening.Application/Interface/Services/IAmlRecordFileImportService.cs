using AmlScreening.Application.Models;

namespace AmlScreening.Application.Interface.Services
{
    public interface IAmlRecordFileImportService
    {
        Task<AmlRecordFileImportResult>ImportAsync(Stream stream,string fileName, CancellationToken cancellationToken = default);
    }
}
