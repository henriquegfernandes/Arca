using Arca.Application.Tenancy;
using Arca.Application.Common;

namespace Arca.Application.Abstractions.Tenancy;

public interface ITenantManagementRepository
{
    Task<PagedResult<TenantSummaryDto>> ListTenantsAsync(PageRequest pageRequest, CancellationToken cancellationToken = default);

    Task<TenantDetailsDto?> GetTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<PagedResult<StoreSummaryDto>> ListStoresAsync(Guid tenantId, PageRequest pageRequest, CancellationToken cancellationToken = default);

    Task<bool> TenantExistsAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<bool> StoreCodeExistsAsync(Guid tenantId, string code, Guid? exceptStoreId = null, CancellationToken cancellationToken = default);

    Task<StoreSummaryDto> CreateStoreAsync(CreateStoreCommand command, CancellationToken cancellationToken = default);

    Task<StoreSummaryDto?> UpdateStoreAsync(UpdateStoreCommand command, CancellationToken cancellationToken = default);

    Task<bool> DisableStoreAsync(Guid tenantId, Guid storeId, CancellationToken cancellationToken = default);
}
