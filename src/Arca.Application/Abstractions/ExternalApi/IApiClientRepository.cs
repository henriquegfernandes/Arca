using Arca.Application.Common;
using Arca.Application.ExternalApi;

namespace Arca.Application.Abstractions.ExternalApi;

public interface IApiClientRepository
{
    Task<bool> TenantExistsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<bool> StoreBelongsToTenantAsync(Guid tenantId, Guid storeId, CancellationToken cancellationToken = default);
    Task<ApiClientDto> CreateAsync(CreateApiClientCommand command, string apiKeyHash, CancellationToken cancellationToken = default);
    Task<PagedResult<ApiClientDto>> ListAsync(Guid tenantId, PageRequest pageRequest, CancellationToken cancellationToken = default);
    Task<ApiClientDto?> UpdateAsync(UpdateApiClientCommand command, CancellationToken cancellationToken = default);
    Task<bool> DisableAsync(Guid tenantId, Guid apiClientId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(DeleteApiClientCommand command, CancellationToken cancellationToken = default);
    Task<ExternalApiClientContext?> AuthenticateAsync(string apiKeyHash, CancellationToken cancellationToken = default);
    Task TouchLastUsedAsync(Guid apiClientId, CancellationToken cancellationToken = default);
    Task LogRequestAsync(ExternalApiRequestLogData requestLog, CancellationToken cancellationToken = default);
}
