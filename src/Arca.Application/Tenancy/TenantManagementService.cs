using Arca.Application.Abstractions.Tenancy;
using Arca.Application.Common;

namespace Arca.Application.Tenancy;

public sealed class TenantManagementService(ITenantManagementRepository repository)
{
    public async Task<Result<PagedResult<TenantSummaryDto>>> ListTenantsAsync(
        PageRequest pageRequest,
        CancellationToken cancellationToken = default)
    {
        var tenants = await repository.ListTenantsAsync(pageRequest, cancellationToken);
        return Result<PagedResult<TenantSummaryDto>>.Success(tenants);
    }

    public async Task<Result<TenantDetailsDto>> GetTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<TenantDetailsDto>.Failure("TenantId is required.");
        }

        var tenant = await repository.GetTenantAsync(tenantId, cancellationToken);
        return tenant is null
            ? Result<TenantDetailsDto>.Failure("Tenant was not found.")
            : Result<TenantDetailsDto>.Success(tenant);
    }

    public async Task<Result<TenantDetailsDto>> UpdateTenantAsync(
        UpdateTenantCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.TenantId == Guid.Empty)
        {
            return Result<TenantDetailsDto>.Failure("TenantId is required.");
        }

        var validationError = await ValidateTenantAsync(command, cancellationToken);
        if (validationError is not null)
        {
            return Result<TenantDetailsDto>.Failure(validationError);
        }

        var normalizedCommand = new UpdateTenantCommand
        {
            TenantId = command.TenantId,
            Company = new CompanySetupStep
            {
                Name = command.Company.Name.Trim(),
                LegalName = TrimToNull(command.Company.LegalName),
                Document = TrimToNull(command.Company.Document),
                Slug = NormalizeSlug(command.Company.Slug),
                Email = TrimToNull(command.Company.Email),
                Phone = TrimToNull(command.Company.Phone),
                MainSegment = TrimToNull(command.Company.MainSegment)
            },
            Settings = new TenantSettingsSetupStep
            {
                Currency = command.Settings.Currency.Trim().ToUpperInvariant(),
                TimeZone = command.Settings.TimeZone.Trim(),
                DefaultLanguage = command.Settings.DefaultLanguage.Trim(),
                AllowMultipleStores = command.Settings.AllowMultipleStores,
                AllowBatchControl = command.Settings.AllowBatchControl,
                AllowExpirationControl = command.Settings.AllowExpirationControl,
                AllowStoreSpecificPricing = command.Settings.AllowStoreSpecificPricing
            },
            PrimaryStoreId = command.PrimaryStoreId,
            RequestedByUserId = command.RequestedByUserId,
            IpAddress = command.IpAddress,
            UserAgent = command.UserAgent
        };

        var tenant = await repository.UpdateTenantAsync(normalizedCommand, cancellationToken);
        return tenant is null
            ? Result<TenantDetailsDto>.Failure("Tenant was not found.")
            : Result<TenantDetailsDto>.Success(tenant);
    }

    public async Task<Result<PagedResult<StoreSummaryDto>>> ListStoresAsync(
        Guid tenantId,
        PageRequest pageRequest,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<PagedResult<StoreSummaryDto>>.Failure("TenantId is required.");
        }

        var stores = await repository.ListStoresAsync(tenantId, pageRequest, cancellationToken);
        return Result<PagedResult<StoreSummaryDto>>.Success(stores);
    }

    public async Task<Result<StoreSummaryDto>> CreateStoreAsync(
        CreateStoreCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationError = await ValidateStoreAsync(
            command.TenantId,
            null,
            command.Name,
            command.Code,
            command.Type,
            command.Email,
            cancellationToken);

        if (validationError is not null)
        {
            return Result<StoreSummaryDto>.Failure(validationError);
        }

        var store = await repository.CreateStoreAsync(NormalizeCreateStoreCommand(command), cancellationToken);
        return Result<StoreSummaryDto>.Success(store);
    }

    public async Task<Result<StoreSummaryDto>> UpdateStoreAsync(
        UpdateStoreCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.StoreId == Guid.Empty)
        {
            return Result<StoreSummaryDto>.Failure("StoreId is required.");
        }

        var validationError = await ValidateStoreAsync(
            command.TenantId,
            command.StoreId,
            command.Name,
            command.Code,
            command.Type,
            command.Email,
            cancellationToken);

        if (validationError is not null)
        {
            return Result<StoreSummaryDto>.Failure(validationError);
        }

        var store = await repository.UpdateStoreAsync(NormalizeUpdateStoreCommand(command), cancellationToken);
        return store is null
            ? Result<StoreSummaryDto>.Failure("Store was not found.")
            : Result<StoreSummaryDto>.Success(store);
    }

    public async Task<Result> DisableStoreAsync(
        Guid tenantId,
        Guid storeId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || storeId == Guid.Empty)
        {
            return Result.Failure("TenantId and StoreId are required.");
        }

        var disabled = await repository.DisableStoreAsync(tenantId, storeId, cancellationToken);
        return disabled ? Result.Success() : Result.Failure("Store was not found.");
    }

    public async Task<Result> ChangeTenantStatusAsync(
        ChangeTenantStatusCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.TenantId == Guid.Empty)
        {
            return Result.Failure("TenantId is required.");
        }

        var changed = await repository.SetTenantActiveAsync(
            command.TenantId,
            command.IsActive,
            command.RequestedByUserId,
            command.IpAddress,
            command.UserAgent,
            cancellationToken);

        return changed ? Result.Success() : Result.Failure("Tenant was not found.");
    }

    public async Task<Result> ChangeStoreStatusAsync(
        ChangeStoreStatusCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.TenantId == Guid.Empty || command.StoreId == Guid.Empty)
        {
            return Result.Failure("TenantId and StoreId are required.");
        }

        var changed = await repository.SetStoreActiveAsync(
            command.TenantId,
            command.StoreId,
            command.IsActive,
            command.RequestedByUserId,
            command.IpAddress,
            command.UserAgent,
            cancellationToken);

        return changed ? Result.Success() : Result.Failure("Store was not found.");
    }

    private async Task<string?> ValidateStoreAsync(
        Guid tenantId,
        Guid? storeId,
        string name,
        string code,
        string type,
        string? email,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
        {
            return "TenantId is required.";
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return "Store name is required.";
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return "Store code is required.";
        }

        if (string.IsNullOrWhiteSpace(type))
        {
            return "Store type is required.";
        }

        if (!string.IsNullOrWhiteSpace(email) && !email.Contains('@', StringComparison.Ordinal))
        {
            return "Store email is invalid.";
        }

        if (!await repository.TenantExistsAsync(tenantId, cancellationToken))
        {
            return "Tenant was not found.";
        }

        if (await repository.StoreCodeExistsAsync(tenantId, code.Trim().ToUpperInvariant(), storeId, cancellationToken))
        {
            return "Store code is already in use for this tenant.";
        }

        return null;
    }

    private async Task<string?> ValidateTenantAsync(
        UpdateTenantCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Company.Name))
        {
            return "Company name is required.";
        }

        if (string.IsNullOrWhiteSpace(command.Company.Slug))
        {
            return "Company slug is required.";
        }

        var slug = NormalizeSlug(command.Company.Slug);
        if (await repository.TenantSlugExistsAsync(slug, command.TenantId, cancellationToken))
        {
            return "Tenant slug is already in use.";
        }

        if (string.IsNullOrWhiteSpace(command.Settings.Currency))
        {
            return "Currency is required.";
        }

        if (string.IsNullOrWhiteSpace(command.Settings.TimeZone))
        {
            return "Time zone is required.";
        }

        if (string.IsNullOrWhiteSpace(command.Settings.DefaultLanguage))
        {
            return "Default language is required.";
        }

        if (command.PrimaryStoreId.HasValue
            && !await repository.StoreBelongsToTenantAsync(command.TenantId, command.PrimaryStoreId.Value, cancellationToken))
        {
            return "Primary store must belong to this tenant.";
        }

        return null;
    }

    private static CreateStoreCommand NormalizeCreateStoreCommand(CreateStoreCommand command) => new()
    {
        TenantId = command.TenantId,
        Name = command.Name.Trim(),
        Code = command.Code.Trim().ToUpperInvariant(),
        Document = TrimToNull(command.Document),
        Phone = TrimToNull(command.Phone),
        Email = TrimToNull(command.Email),
        AddressLine = TrimToNull(command.AddressLine),
        City = TrimToNull(command.City),
        State = TrimToNull(command.State),
        ZipCode = TrimToNull(command.ZipCode),
        Type = command.Type.Trim(),
        RequestedByUserId = command.RequestedByUserId,
        IpAddress = command.IpAddress,
        UserAgent = command.UserAgent
    };

    private static UpdateStoreCommand NormalizeUpdateStoreCommand(UpdateStoreCommand command) => new()
    {
        TenantId = command.TenantId,
        StoreId = command.StoreId,
        Name = command.Name.Trim(),
        Code = command.Code.Trim().ToUpperInvariant(),
        Document = TrimToNull(command.Document),
        Phone = TrimToNull(command.Phone),
        Email = TrimToNull(command.Email),
        AddressLine = TrimToNull(command.AddressLine),
        City = TrimToNull(command.City),
        State = TrimToNull(command.State),
        ZipCode = TrimToNull(command.ZipCode),
        Type = command.Type.Trim(),
        RequestedByUserId = command.RequestedByUserId,
        IpAddress = command.IpAddress,
        UserAgent = command.UserAgent
    };

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeSlug(string value) =>
        value.Trim().ToLowerInvariant();
}
