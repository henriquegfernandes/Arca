using Arca.Application.Abstractions.Auth;
using Arca.Application.Abstractions.Catalog;
using Arca.Application.Security;
using Arca.Infrastructure.Auth;
using Arca.Infrastructure.Catalog;
using Arca.Infrastructure.Database;
using Arca.Infrastructure.Seed;
using Arca.Infrastructure.Tenancy;
using Microsoft.Extensions.DependencyInjection;

namespace Arca.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IDbConnectionFactory, NpgsqlConnectionFactory>();
        services.AddScoped<IDatabaseMigrationRunner, DatabaseMigrationRunner>();
        services.AddScoped<IUserAuthenticationRepository, DapperUserAuthenticationRepository>();
        services.AddScoped<Arca.Application.Abstractions.Tenancy.ITenantSetupRepository, DapperTenantSetupRepository>();
        services.AddScoped<IProductCatalogRepository, DapperProductCatalogRepository>();
        services.AddScoped<IPermissionService, DapperPermissionService>();
        services.AddScoped<ITenantAccessService, DapperTenantAccessService>();
        services.AddScoped<ISuperAdminSeedService, SuperAdminSeedService>();
        services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();

        return services;
    }
}
