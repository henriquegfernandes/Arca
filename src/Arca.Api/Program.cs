using System.Threading.RateLimiting;
using Arca.Api.Middlewares;
using Arca.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);
AddKeyPerFileSecrets(builder.Configuration, builder.Environment);

if (!builder.Environment.IsDevelopment())
{
    ValidateProductionConfiguration(builder.Configuration);
}

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = builder.Configuration.GetValue<long>("Security:MaxRequestBodyBytes", 1024 * 1024);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("Security:RequestHeadersTimeoutSeconds", 15));
});

builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddInfrastructure();
builder.Services.AddScoped<IExternalApiClientContextAccessor, ExternalApiClientContextAccessor>();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue("RateLimiting:ExternalApi:PermitLimit", 120),
                Window = TimeSpan.FromMinutes(builder.Configuration.GetValue("RateLimiting:ExternalApi:WindowMinutes", 1)),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("external-api", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue("RateLimiting:ExternalApi:PermitLimit", 120),
                Window = TimeSpan.FromMinutes(builder.Configuration.GetValue("RateLimiting:ExternalApi:WindowMinutes", 1)),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

var app = builder.Build();
app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.MapHealthChecks("/health");
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseRateLimiter();
app.UseMiddleware<ExternalApiRequestLoggingMiddleware>();
app.UseMiddleware<ExternalApiAuthenticationMiddleware>();
app.MapControllers();
app.Run();

static void ValidateProductionConfiguration(IConfiguration configuration)
{
    if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("DefaultConnection")))
    {
        throw new InvalidOperationException("Production database connection string is required.");
    }
}

static void AddKeyPerFileSecrets(ConfigurationManager configuration, IWebHostEnvironment environment)
{
    var enabled = configuration.GetValue("Secrets:KeyPerFile:Enabled", !environment.IsDevelopment());
    if (!enabled)
    {
        return;
    }

    var path = configuration["Secrets:KeyPerFile:Path"]
        ?? Environment.GetEnvironmentVariable("ARCA_SECRETS_PATH")
        ?? "/run/secrets";

    if (!Directory.Exists(path))
    {
        return;
    }

    var secrets = Directory
        .EnumerateFiles(path)
        .Where(File.Exists)
        .ToDictionary<string, string, string?>(
            file => Path.GetFileName(file).Replace("__", ":", StringComparison.Ordinal),
            file => File.ReadAllText(file).Trim(),
            StringComparer.OrdinalIgnoreCase);

    configuration.AddInMemoryCollection(secrets);
}
