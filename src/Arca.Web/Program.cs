using System.Threading.RateLimiting;
using Arca.Application.Auth;
using Arca.Application.Catalog;
using Arca.Application.ExternalApi;
using Arca.Application.Inventory;
using Arca.Application.Security;
using Arca.Application.Tenancy;
using Arca.Application.Users;
using Arca.Infrastructure;
using Arca.Infrastructure.Database;
using Arca.Infrastructure.Seed;
using Arca.Web.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
AddKeyPerFileSecrets(builder.Configuration, builder.Environment);

if (!builder.Environment.IsDevelopment())
{
    ValidateProductionConfiguration(builder.Configuration);
}

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = builder.Configuration.GetValue<long>("Security:MaxRequestBodyBytes", 10 * 1024 * 1024);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("Security:RequestHeadersTimeoutSeconds", 15));
});

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

builder.Services.AddHealthChecks();
builder.Services.AddInfrastructure();
builder.Services.AddScoped<AuthenticateUserUseCase>();
builder.Services.AddScoped<TenantSetupService>();
builder.Services.AddScoped<TenantManagementService>();
builder.Services.AddScoped<UserManagementService>();
builder.Services.AddScoped<RoleManagementService>();
builder.Services.AddScoped<UserProvisioningService>();
builder.Services.AddScoped<CatalogTemplateSeeder>();
builder.Services.AddScoped<ProductCatalogService>();
builder.Services.AddScoped<CatalogManagementService>();
builder.Services.AddScoped<ProductImageService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<ApiClientService>();
builder.Services.AddSingleton<IProductVariantGenerator, ProductVariantGenerator>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("admin", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.Identity?.IsAuthenticated == true
                ? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown"
                : context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue("RateLimiting:Admin:PermitLimit", 300),
                Window = TimeSpan.FromMinutes(builder.Configuration.GetValue("RateLimiting:Admin:WindowMinutes", 1)),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue("RateLimiting:Auth:PermitLimit", 20),
                Window = TimeSpan.FromMinutes(builder.Configuration.GetValue("RateLimiting:Auth:WindowMinutes", 1)),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = builder.Configuration["Authentication:CookieName"] ?? "Arca.Admin";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(builder.Configuration.GetValue("Authentication:ExpireMinutes", 480));
        options.SlidingExpiration = true;
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/access-denied";
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SuperAdmin", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("arca:is_super_admin", bool.TrueString);
    });

    foreach (var permission in KnownPermissions.All)
    {
        options.AddPolicy(permission.Name, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.Requirements.Add(new PermissionRequirement(permission.Name));
        });
    }
});

var app = builder.Build();

if (app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", false))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<IDatabaseMigrationRunner>().MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<ISuperAdminSeedService>().SeedAsync();
}

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseRouting();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseStaticFiles();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapControllers().RequireRateLimiting("admin");
app.MapDefaultControllerRoute();
app.Run();

static void ValidateProductionConfiguration(IConfiguration configuration)
{
    if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("DefaultConnection")))
    {
        throw new InvalidOperationException("Production database connection string is required.");
    }

    var storageProvider = configuration["Storage:Provider"];
    if (!string.Equals(storageProvider, "S3", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("Production storage must use the S3 provider.");
    }

    var requiredKeys = new[]
    {
        "Storage:S3:BucketName",
        "Storage:S3:Region",
        "Storage:S3:AccessKey",
        "Storage:S3:SecretKey"
    };

    foreach (var key in requiredKeys)
    {
        if (string.IsNullOrWhiteSpace(configuration[key]))
        {
            throw new InvalidOperationException($"Production configuration value '{key}' is required.");
        }
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
