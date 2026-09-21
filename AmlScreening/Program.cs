using AmlScreening.Application;
using AmlScreening.Infrastructure;
using AmlScreening.Portal.Extension;
using AmlScreening.Portal.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Controllers;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
if (!builder.Environment.IsDevelopment())
    ValidateHostedConfiguration(builder.Configuration, builder.Environment);


//MVC
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(
        new Microsoft.AspNetCore.Mvc.Authorization.AuthorizeFilter(
            AppPermissions.Portal.Access));
});

builder.Services.AddCurrentUserAuthorization();
builder.Services.AddCustomDataProtection(builder.Configuration, builder.Environment);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<PortalCookieEvents>();


builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)

    .AddCookie(options =>
    {
        options.Cookie.Name = "AMLScreening.Auth.v2";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
        options.SlidingExpiration = true;
        options.EventsType = typeof(PortalCookieEvents);
    });
builder.Services.AddAuthorization(options =>
{
    foreach (var permission in AppPermissions.All)
    {
        options.AddPolicy(
            permission,
            policy =>
            {
                policy.RequireAuthenticatedUser();

                policy.RequireClaim(
                    AppPermissions.ClaimType,
                    permission);
            });
    }
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        await context.HttpContext.Response.WriteAsync(
            "Too many login attempts for this username and IP address. Please wait one minute and try again.",
            cancellationToken);
    };

    options.AddPolicy("login", httpContext =>
    {
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown-ip";
        var username = httpContext.Items["loginusername"]?.ToString() ?? "unknown-user";
        var partitionKey = $"{ipAddress}|{username}";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Read the submitted username before the rate limiter creates its partition.
// The form remains available to MVC after ReadFormAsync has been called here.
app.Use(async (context, next) =>
{
    var action = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
    var isLoginPost = HttpMethods.IsPost(context.Request.Method)
        && action?.ControllerName.Equals("Account", StringComparison.OrdinalIgnoreCase) == true
        && action.ActionName.Equals("Login", StringComparison.OrdinalIgnoreCase)
        && context.Request.HasFormContentType;

    if (isLoginPost)
    {
        var form = await context.Request.ReadFormAsync();
        context.Items["loginusername"] = form["UserName"].ToString().Trim().ToUpperInvariant();
    }

    await next();
});

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}")
    .WithStaticAssets();

app.Run();

static void ValidateHostedConfiguration(
    IConfiguration configuration,
    IHostEnvironment environment)
{
    var connectionString = configuration.GetConnectionString("Screening");
    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException(
            "ConnectionStrings__AMLDatabase must be supplied by the host secret store.");

    var allowedHosts = configuration["AllowedHosts"];
    if (string.IsNullOrWhiteSpace(allowedHosts) ||
        allowedHosts.Contains("__SET_", StringComparison.OrdinalIgnoreCase) ||
        allowedHosts == "*")
    {
        throw new InvalidOperationException(
            $"A real AllowedHosts value is required for {environment.EnvironmentName}.");
    }

    var keysPath = configuration["DataProtection:KeysPath"];
    if (string.IsNullOrWhiteSpace(keysPath) ||
        keysPath.Contains("__SET_", StringComparison.OrdinalIgnoreCase) ||
        !Path.IsPathFullyQualified(keysPath))
    {
        throw new InvalidOperationException(
            $"A fully-qualified DataProtection__KeysPath is required for {environment.EnvironmentName}.");
    }

    try
    {
        Directory.CreateDirectory(keysPath);
    }
    catch (Exception exception)
    {
        throw new InvalidOperationException(
            $"The data-protection directory '{keysPath}' could not be prepared.",
            exception);
    }
}
