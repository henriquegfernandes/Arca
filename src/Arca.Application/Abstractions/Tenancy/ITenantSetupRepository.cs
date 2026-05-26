using Arca.Application.Tenancy;

namespace Arca.Application.Abstractions.Tenancy;

public interface ITenantSetupRepository
{
    Task<bool> TenantSlugExistsAsync(string slug, CancellationToken cancellationToken = default);
    Task<TenantSetupResult> CreateAsync(TenantSetupData setupData, CancellationToken cancellationToken = default);
}
