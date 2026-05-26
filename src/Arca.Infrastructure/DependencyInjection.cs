using Arca.Application.Abstractions.Auth;
using Arca.Application.Security;
using Arca.Infrastructure.Auth;
using Arca.Infrastructure.Database;
using Arca.Infrastructure.Seed;
using Microsoft.Extensions.DependencyInjection;

namespace Arca.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IDbConnectionFactory, NpgsqlConnectionFactory>();
        services.AddScoped<IDatabaseMigrationRunner, DatabaseMigrationRunner>();
        services.AddScoped<IUserAuthenticationRepository, DapperUserAuthenticationRepository>();
        services.AddScoped<IPermissionService, DapperPermissionService>();
        services.AddScoped<ITenantAccessService, DapperTenantAccessService>();
        services.AddScoped<ISuperAdminSeedService, SuperAdminSeedService>();
        services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();

        return services;
    }
}
