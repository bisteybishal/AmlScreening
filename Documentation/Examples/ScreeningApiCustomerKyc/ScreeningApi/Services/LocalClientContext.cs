using ScreeningApi.Application.Interface.Services;

namespace ScreeningApi.Services;

// Temporary local-only implementation. Replace with verified OAuth claims later.
public sealed class LocalClientContext(
    IHostEnvironment environment, IConfiguration configuration) : ICurrentClientContext
{
    public int ClientId
    {
        get
        {
            if (!environment.IsDevelopment())
                throw new InvalidOperationException("Authentication is required outside local Development.");
            int clientId = configuration.GetValue<int>("LocalTesting:ClientId");
            return clientId > 0 ? clientId
                : throw new InvalidOperationException("Configure LocalTesting:ClientId.");
        }
    }
}
