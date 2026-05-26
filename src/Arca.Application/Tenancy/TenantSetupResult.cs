namespace Arca.Application.Tenancy;

public sealed record TenantSetupResult(
    Guid TenantId,
    IReadOnlyCollection<Guid> StoreIds,
    Guid AdministratorUserId,
    string CatalogTemplate);
