using Arca.Application.Abstractions.Auth;
using Arca.Application.Abstractions.Catalog;
using Arca.Application.Abstractions.ExternalApi;
using Arca.Application.Abstractions.Inventory;
using Arca.Application.Abstractions.Tenancy;
using Arca.Application.Abstractions.Users;
using Arca.Application.Security;
using Arca.Application.Storage;
using Arca.Infrastructure.Auth;
using Arca.Infrastructure.Catalog;
using Arca.Infrastructure.Database;
using Arca.Infrastructure.ExternalApi;
using Arca.Infrastructure.Inventory;
using Arca.Infrastructure.Seed;
using Arca.Infrastructure.Storage;
using Arca.Infrastructure.Tenancy;
using Arca.Infrastructure.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Arca.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IDbConnectionFactory, NpgsqlConnectionFactory>();
        services.AddScoped<IDatabaseMigrationRunner, DatabaseMigrationRunner>();
        services.AddScoped<IUserAuthenticationRepository, DapperUserAuthenticationRepository>();
        services.AddScoped<ITenantSetupRepository, DapperTenantSetupRepository>();
        services.AddScoped<ITenantManagementRepository, DapperTenantManagementRepository>();
        services.AddScoped<IUserManagementRepository, DapperUserManagementRepository>();
        services.AddScoped<IRoleManagementRepository, DapperRoleManagementRepository>();
        services.AddScoped<IProductCatalogRepository, DapperProductCatalogRepository>();
        services.AddScoped<ICatalogManagementRepository, DapperCatalogManagementRepository>();
        services.AddScoped<IProductImageRepository, DapperProductImageRepository>();
        services.AddScoped<IInventoryRepository, DapperInventoryRepository>();
        services.AddScoped<IApiClientRepository, DapperApiClientRepository>();
        services.AddScoped<IExternalCatalogRepository, DapperExternalCatalogRepository>();
        services.AddScoped<IPermissionService, DapperPermissionService>();
        services.AddScoped<ITenantAccessService, DapperTenantAccessService>();
        services.AddScoped<ISuperAdminSeedService, SuperAdminSeedService>();
        services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
        services.AddSingleton<IApiKeyHasher, Sha256ApiKeyHasher>();
        services.AddScoped<IFileStorageService>(provider =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            var storageProvider = configuration["Storage:Provider"] ?? "Local";

            return storageProvider.Equals("S3", StringComparison.OrdinalIgnoreCase)
                ? new S3FileStorageService(configuration)
                : new LocalFileStorageService(configuration);
        });

        return services;
    }
}
