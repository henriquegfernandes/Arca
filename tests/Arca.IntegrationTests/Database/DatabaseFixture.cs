using System.Diagnostics;
using Arca.Infrastructure.Database;
using Dapper;
using Npgsql;

namespace Arca.IntegrationTests.Database;

public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly string _databaseName = $"arca_it_{Guid.NewGuid():N}";

    public string ConnectionString { get; private set; } = string.Empty;
    public Guid TenantId { get; } = Guid.NewGuid();
    public Guid UserId { get; } = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await EnsurePostgresIsRunningAsync();

        var adminConnectionString = GetAdminConnectionString();
        await CreateDatabaseAsync(adminConnectionString);

        ConnectionString = BuildConnectionString(_databaseName);
        await ApplyMigrationsAsync();
        await SeedTenantAndUserAsync();
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            return;
        }

        var adminConnectionString = GetAdminConnectionString();
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();

        await connection.ExecuteAsync(
            """
            SELECT pg_terminate_backend(pid)
            FROM pg_stat_activity
            WHERE datname = @DatabaseName AND pid <> pg_backend_pid();
            """,
            new { DatabaseName = _databaseName });

        await connection.ExecuteAsync($"DROP DATABASE IF EXISTS \"{_databaseName}\";");
    }

    public IDbConnectionFactory CreateConnectionFactory() => new TestDbConnectionFactory(ConnectionString);

    private static async Task EnsurePostgresIsRunningAsync()
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "docker",
            ArgumentList = { "compose", "up", "-d", "postgres" },
            WorkingDirectory = FindRepositoryRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });

        if (process is null)
        {
            throw new InvalidOperationException("Could not start docker compose.");
        }

        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"docker compose up failed: {error}");
        }

        var adminConnectionString = GetAdminConnectionString();
        var deadline = DateTime.UtcNow.AddSeconds(30);
        Exception? lastException = null;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await using var connection = new NpgsqlConnection(adminConnectionString);
                await connection.OpenAsync();
                return;
            }
            catch (Exception exception)
            {
                lastException = exception;
                await Task.Delay(500);
            }
        }

        throw new InvalidOperationException("PostgreSQL did not become ready in time.", lastException);
    }

    private async Task CreateDatabaseAsync(string adminConnectionString)
    {
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync($"CREATE DATABASE \"{_databaseName}\";");
    }

    private async Task ApplyMigrationsAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        foreach (var migrationFile in Directory.EnumerateFiles(FindMigrationsPath(), "*.sql").OrderBy(Path.GetFileName))
        {
            var sql = await File.ReadAllTextAsync(migrationFile);
            await connection.ExecuteAsync(sql);
        }
    }

    private async Task SeedTenantAndUserAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        await connection.ExecuteAsync(
            """
            INSERT INTO tenant (
                id, name, legal_name, document, slug, is_active, setup_status,
                created_at, updated_at, contact_email, phone, main_segment
            )
            VALUES (
                @TenantId, 'Integration Tenant', NULL, NULL, @Slug, TRUE, 'Completed',
                @CreatedAt, NULL, NULL, NULL, NULL
            );

            INSERT INTO app_user (
                id, full_name, email, normalized_email, phone, password_hash,
                is_active, email_confirmed, last_login_at, created_at, updated_at
            )
            VALUES (
                @UserId, 'Integration User', @Email, @NormalizedEmail, NULL, 'not-used',
                TRUE, TRUE, NULL, @CreatedAt, NULL
            );

            INSERT INTO tenant_settings (
                id, tenant_id, currency, time_zone, default_language,
                allow_multiple_stores, allow_batch_control, allow_expiration_control,
                allow_store_specific_pricing, created_at, updated_at
            )
            VALUES (
                @TenantSettingsId, @TenantId, 'BRL', 'America/Sao_Paulo', 'pt-BR',
                TRUE, FALSE, FALSE, TRUE, @CreatedAt, NULL
            );
            """,
            new
            {
                TenantId,
                TenantSettingsId = Guid.NewGuid(),
                Slug = $"integration-{Guid.NewGuid():N}",
                UserId,
                Email = $"integration-{Guid.NewGuid():N}@arca.local",
                NormalizedEmail = $"INTEGRATION-{Guid.NewGuid():N}@ARCA.LOCAL",
                CreatedAt = DateTime.UtcNow
            });
    }

    private static string GetAdminConnectionString() =>
        Environment.GetEnvironmentVariable("ARCA_TEST_ADMIN_CONNECTION")
        ?? "Host=localhost;Port=5433;Database=postgres;Username=postgres;Password=postgres;Include Error Detail=true";

    private static string BuildConnectionString(string databaseName)
    {
        var builder = new NpgsqlConnectionStringBuilder(GetAdminConnectionString())
        {
            Database = databaseName
        };

        return builder.ConnectionString;
    }

    private static string FindMigrationsPath()
    {
        var root = FindRepositoryRoot();
        var migrationsPath = Path.Combine(root, "src", "Arca.Infrastructure", "Migrations");
        if (!Directory.Exists(migrationsPath))
        {
            throw new DirectoryNotFoundException($"Migrations path was not found: {migrationsPath}");
        }

        return migrationsPath;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Arca.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private sealed class TestDbConnectionFactory(string connectionString) : IDbConnectionFactory
    {
        public System.Data.IDbConnection CreateConnection() => new NpgsqlConnection(connectionString);
    }
}
