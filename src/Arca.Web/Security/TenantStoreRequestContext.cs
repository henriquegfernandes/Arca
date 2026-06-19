using Arca.Application.Security;

namespace Arca.Web.Security;

public sealed class TenantStoreRequestContext : ICurrentTenantService, ICurrentStoreService
{
    public Guid? TenantId { get; private set; }
    public string? TenantSlug { get; private set; }
    public Guid? StoreId { get; private set; }
    public string? StoreCode { get; private set; }

    public void SetTenant(Guid? tenantId, string? tenantSlug = null)
    {
        TenantId = tenantId;
        TenantSlug = tenantSlug;
    }

    public void SetStore(Guid? storeId, string? storeCode = null)
    {
        StoreId = storeId;
        StoreCode = storeCode;
    }
}
