using Arca.Infrastructure.Database;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Arca.Api.Middlewares;

public sealed class ApiDatabaseHealthCheck(IDbConnectionFactory connectionFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = connectionFactory.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            command.ExecuteScalar();
            return HealthCheckResult.Healthy("Database is reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database is unreachable.", ex);
        }
    }
}
