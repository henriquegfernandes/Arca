namespace Arca.Application.Security;

public interface ITenantAccessService
{
    Task<bool> UserHasAccessToTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<bool> UserHasAccessToStoreAsync(Guid userId, Guid tenantId, Guid storeId, CancellationToken cancellationToken = default);
}
