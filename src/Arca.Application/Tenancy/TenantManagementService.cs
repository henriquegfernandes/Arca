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
}
