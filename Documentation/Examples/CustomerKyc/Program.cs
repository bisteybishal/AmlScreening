using System.Security.Cryptography.X509Certificates;
using CustomerKyc.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using System.Threading.RateLimiting;

var exportSchema = args.Contains("--export-auth-schema");
var registerClient = args.Contains("--register-client");
var hostArgs = args.Where(a => a != "--export-auth-schema" && a != "--register-client").ToArray();
var builder = WebApplication.CreateBuilder(hostArgs);
var connectionString = builder.Configuration.GetConnectionString("AMLDatabase")
    ?? throw new InvalidOperationException("Configure ConnectionStrings:AMLDatabase.");
if (string.IsNullOrWhiteSpace(connectionString)) throw new InvalidOperationException("Configure AMLDatabase.");

builder.Services.AddDbContext<AuthDbContext>(options =>
{
    options.UseSqlServer(connectionString);
    options.UseOpenIddict();
});
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddScoped<KycRepository>();
builder.Services.AddScoped<PartnerRegistry>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddFixedWindowLimiter("token", limiter =>
    {
        limiter.PermitLimit = 30; limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0; limiter.AutoReplenishment = true;
    });
    options.AddPolicy("kyc", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.GetClaim(OpenIddictConstants.Claims.Subject) ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0,
                AutoReplenishment = true
            }));
});

X509Certificate2? encryptionCertificate = null;
X509Certificate2? signingCertificate = null;
var protection = builder.Services.AddDataProtection().SetApplicationName("CustomerKyc");
if (!builder.Environment.IsDevelopment() && !exportSchema)
{
    X509Certificate2 Load(string name) => X509CertificateLoader.LoadPkcs12FromFile(
        builder.Configuration[$"Certificates:{name}:Path"] ?? throw new InvalidOperationException($"Configure {name} certificate."),
        builder.Configuration[$"Certificates:{name}:Password"]);
    encryptionCertificate = Load("Encryption");
    signingCertificate = Load("Signing");
    protection.PersistKeysToFileSystem(new DirectoryInfo(
        builder.Configuration["DataProtection:Directory"] ?? throw new InvalidOperationException("Configure protected key directory.")))
        .ProtectKeysWithCertificate(encryptionCertificate);
}

builder.Services.AddOpenIddict()
    .AddCore(options => options.UseEntityFrameworkCore().UseDbContext<AuthDbContext>())
    .AddServer(options =>
    {
        options.SetIssuer(new Uri(builder.Configuration["OAuth:Issuer"] ?? throw new InvalidOperationException("Configure OAuth issuer.")));
        options.SetTokenEndpointUris("/connect/token");
        options.AllowClientCredentialsFlow();
        options.RegisterScopes("customer.screening");
        options.UseReferenceAccessTokens();
        options.SetAccessTokenLifetime(TimeSpan.FromMinutes(10));
        if (builder.Environment.IsDevelopment() || exportSchema)
            options.AddDevelopmentEncryptionCertificate().AddDevelopmentSigningCertificate();
        else
            options.AddEncryptionCertificate(encryptionCertificate!).AddSigningCertificate(signingCertificate!);
        options.UseAspNetCore().EnableTokenEndpointPassthrough();
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.AddAudiences("customer-kyc");
        options.EnableTokenEntryValidation();
        options.UseAspNetCore();
    });
builder.Services.AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("customer-screening", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context => context.User.HasScope("customer.screening"));
    });
});

var app = builder.Build();
// Explicit export only: NEVER migrate/create application tables at startup.
if (exportSchema)
{
    using var scope = app.Services.CreateScope();
    Console.WriteLine(scope.ServiceProvider.GetRequiredService<AuthDbContext>().Database.GenerateCreateScript());
    return;
}
// Explicit operator command, no public registration endpoint.
if (registerClient)
{
    await using var scope = app.Services.CreateAsyncScope();
    await PartnerRegistration.RunAsync(scope.ServiceProvider, builder.Configuration);
    return;
}
app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
await app.RunAsync();
