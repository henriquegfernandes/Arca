namespace Arca.Infrastructure.Database;

public interface IDatabaseMigrationRunner
{
    Task MigrateAsync(CancellationToken cancellationToken = default);
}
