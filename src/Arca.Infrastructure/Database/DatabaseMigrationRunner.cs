using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Arca.Infrastructure.Database;

public sealed class DatabaseMigrationRunner(
    IDbConnectionFactory connectionFactory,
    IConfiguration configuration,
    ILogger<DatabaseMigrationRunner> logger) : IDatabaseMigrationRunner
{
    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        var migrationsPath = Path.Combine(AppContext.BaseDirectory, "Migrations");
        if (!Directory.Exists(migrationsPath))
        {
            logger.LogInformation("No migrations directory found at {MigrationsPath}", migrationsPath);
            return;
        }

        using var connection = OpenConnection();

        await connection.ExecuteAsync(new CommandDefinition(
            """
            CREATE TABLE IF NOT EXISTS schema_migration (
                migration_id VARCHAR(255) PRIMARY KEY,
                applied_at TIMESTAMP NOT NULL
            );
            """,
            cancellationToken: cancellationToken));

        var applied = (await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT migration_id FROM schema_migration;",
            cancellationToken: cancellationToken))).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var migrationFile in Directory.EnumerateFiles(migrationsPath, "*.sql").OrderBy(Path.GetFileName))
        {
            var migrationId = Path.GetFileName(migrationFile);
            if (applied.Contains(migrationId))
            {
                continue;
            }

            var sql = await File.ReadAllTextAsync(migrationFile, cancellationToken);
            using var transaction = connection.BeginTransaction();

            try
            {
                await connection.ExecuteAsync(new CommandDefinition(sql, transaction: transaction, cancellationToken: cancellationToken));
                await connection.ExecuteAsync(new CommandDefinition(
                    "INSERT INTO schema_migration (migration_id, applied_at) VALUES (@MigrationId, @AppliedAt);",
                    new { MigrationId = migrationId, AppliedAt = DateTime.UtcNow },
                    transaction,
                    cancellationToken: cancellationToken));

                transaction.Commit();
                logger.LogInformation("Applied database migration {MigrationId}", migrationId);
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
    }

    private System.Data.IDbConnection OpenConnection()
    {
        var connection = connectionFactory.CreateConnection();

        try
        {
            connection.Open();
            return connection;
        }
        catch (PostgresException exception) when (
            exception.SqlState == "3D000"
            && bool.TryParse(configuration["Database:CreateDatabaseIfMissing"], out var canCreateDatabase)
            && canCreateDatabase)
        {
            connection.Dispose();
            CreateDatabase();

            var retryConnection = connectionFactory.CreateConnection();
            retryConnection.Open();
            return retryConnection;
        }
    }

    private void CreateDatabase()
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var databaseName = builder.Database;
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("Connection string 'DefaultConnection' must include a database name.");
        }

        builder.Database = "postgres";

        using var connection = new NpgsqlConnection(builder.ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE {QuoteIdentifier(databaseName)};";
        command.ExecuteNonQuery();

        logger.LogInformation("Created PostgreSQL database {DatabaseName}", databaseName);
    }

    private static string QuoteIdentifier(string identifier) => "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
}
