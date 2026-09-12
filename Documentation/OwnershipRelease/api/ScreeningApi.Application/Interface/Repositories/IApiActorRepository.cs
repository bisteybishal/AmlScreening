using ScreeningApi.Application.Models;

namespace ScreeningApi.Application.Interface.Repositories;

public interface IApiActorRepository
{
    Task<ApiActor?> FindAsync(string issuer, string subject, CancellationToken cancellationToken);
}

