namespace Arca.Infrastructure.Seed;

public interface ISuperAdminSeedService
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}
