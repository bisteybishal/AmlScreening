using Microsoft.EntityFrameworkCore;
namespace CustomerKyc.Infrastructure;

public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("oauth");
    }
}
