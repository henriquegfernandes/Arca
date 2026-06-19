namespace Arca.Application.Tenancy;

public sealed record TenantSummaryDto(
    Guid Id,
    string Name,
    string Slug,
    string? ContactEmail,
    string? MainSegment,
    bool IsActive,
    string SetupStatus,
    string Currency,
    string TimeZone,
    Guid? PrimaryStoreId,
    int StoreCount,
    DateTime CreatedAt);

public sealed record TenantDetailsDto(
    Guid Id,
    string Name,
    string? LegalName,
    string? Document,
    string Slug,
    string? ContactEmail,
    string? Phone,
    string? MainSegment,
    Guid? PrimaryStoreId,
    bool IsActive,
    string SetupStatus,
    TenantSettingsDto Settings,
    IReadOnlyCollection<StoreSummaryDto> Stores,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record TenantSettingsDto(
    string Currency,
    string TimeZone,
    string DefaultLanguage,
    bool AllowMultipleStores,
    bool AllowBatchControl,
    bool AllowExpirationControl,
    bool AllowStoreSpecificPricing);

public sealed record StoreSummaryDto(
    Guid Id,
    Guid TenantId,
    string Name,
    string Code,
    string Type,
    string? Document,
    string? Phone,
    string? Email,
    string? AddressLine,
    string? City,
    string? State,
    string? ZipCode,
    bool IsActive,
    DateTime CreatedAt);

public sealed class CreateStoreCommand
{
    public Guid TenantId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string? Document { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? AddressLine { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? ZipCode { get; init; }
    public string Type { get; init; } = "Physical";
    public Guid? RequestedByUserId { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
}

public sealed class UpdateStoreCommand
{
    public Guid TenantId { get; init; }
    public Guid StoreId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string? Document { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? AddressLine { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? ZipCode { get; init; }
    public string Type { get; init; } = "Physical";
    public Guid? RequestedByUserId { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
}

public sealed class UpdateTenantCommand
{
    public Guid TenantId { get; init; }
    public CompanySetupStep Company { get; init; } = new();
    public TenantSettingsSetupStep Settings { get; init; } = new();
    public Guid? PrimaryStoreId { get; init; }
    public Guid? RequestedByUserId { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
}

public sealed class ChangeTenantStatusCommand
{
    public Guid TenantId { get; init; }
    public bool IsActive { get; init; }
    public Guid? RequestedByUserId { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
}

public sealed class ChangeStoreStatusCommand
{
    public Guid TenantId { get; init; }
    public Guid StoreId { get; init; }
    public bool IsActive { get; init; }
    public Guid? RequestedByUserId { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
}
