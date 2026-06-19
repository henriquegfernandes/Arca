namespace Arca.Application.Tenancy;

public sealed class CreateTenantSetupCommand
{
    public CompanySetupStep Company { get; init; } = new();
    public TenantSettingsSetupStep Settings { get; init; } = new();
    public List<StoreSetupStep> Stores { get; init; } = [];
    public AdministratorSetupStep Administrator { get; init; } = new();
    public InitialCatalogSetupStep Catalog { get; init; } = new();
    public Guid? RequestedByUserId { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
}

public sealed class CompanySetupStep
{
    public string Name { get; init; } = string.Empty;
    public string? LegalName { get; init; }
    public string? Document { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? MainSegment { get; init; }
}

public sealed class TenantSettingsSetupStep
{
    public string Currency { get; init; } = "BRL";
    public string TimeZone { get; init; } = "America/Sao_Paulo";
    public string DefaultLanguage { get; init; } = "pt-BR";
    public bool AllowMultipleStores { get; init; } = true;
    public bool AllowBatchControl { get; init; }
    public bool AllowExpirationControl { get; init; }
    public bool AllowStoreSpecificPricing { get; init; }
}

public sealed class StoreSetupStep
{
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string? Document { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? AddressLine { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? ZipCode { get; init; }
    public string Type { get; init; } = "Store";
}

public sealed class AdministratorSetupStep
{
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? TemporaryPassword { get; init; }
    public bool SendInviteEmail { get; init; }
}

public sealed class InitialCatalogSetupStep
{
    public string Template { get; init; } = "Custom";
}
