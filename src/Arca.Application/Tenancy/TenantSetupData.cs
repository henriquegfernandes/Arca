namespace Arca.Application.Tenancy;

public sealed record TenantSetupData(
    CreateTenantSetupCommand Command,
    string AdministratorNormalizedEmail,
    string AdministratorPasswordHash,
    CatalogTemplateDefinition CatalogTemplate);
