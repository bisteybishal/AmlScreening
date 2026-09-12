using AmlScreening.Application.Models;

namespace AmlScreening.Application.Interface.Repositories;

public interface IErrorRepository
{
    Task<int> LogErrorAsync(
         ErrorLogModel error,
         CancellationToken cancellationToken = default);
}
