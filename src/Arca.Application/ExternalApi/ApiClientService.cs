using Arca.Application.Abstractions.ExternalApi;
using Arca.Application.Common;

namespace Arca.Application.ExternalApi;

public sealed class ApiClientService(
    IApiClientRepository repository,
    IApiKeyHasher apiKeyHasher)
{
    public async Task<Result<CreateApiClientResult>> CreateAsync(
        CreateApiClientCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationError = await ValidateCreateAsync(command, cancellationToken);
        if (validationError is not null)
        {
            return Result<CreateApiClientResult>.Failure(validationError);
        }

        var permissions = command.Permissions
            .Select(permission => permission.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var apiKey = apiKeyHasher.GenerateApiKey();
        var apiKeyHash = apiKeyHasher.HashApiKey(apiKey);

        var created = await repository.CreateAsync(
            new CreateApiClientCommand
            {
                TenantId = command.TenantId,
                StoreId = command.StoreId,
                Name = command.Name.Trim(),
                Permissions = permissions.ToList()
            },
            apiKeyHash,
            cancellationToken);

        return Result<CreateApiClientResult>.Success(new CreateApiClientResult(
            created.Id,
            created.TenantId,
            created.StoreId,
            created.Name,
            apiKey,
            created.Permissions));
    }

    public async Task<Result<PagedResult<ApiClientDto>>> ListAsync(
        Guid tenantId,
        PageRequest pageRequest,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Result<PagedResult<ApiClientDto>>.Failure("TenantId is required.");
        }

        var clients = await repository.ListAsync(tenantId, pageRequest, cancellationToken);
        return Result<PagedResult<ApiClientDto>>.Success(clients);
    }

    public async Task<Result> DisableAsync(
        Guid tenantId,
        Guid apiClientId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || apiClientId == Guid.Empty)
        {
            return Result.Failure("TenantId and apiClientId are required.");
        }

        var disabled = await repository.DisableAsync(tenantId, apiClientId, cancellationToken);
        return disabled ? Result.Success() : Result.Failure("API client was not found.");
    }

    public async Task<Result> DeleteAsync(
        DeleteApiClientCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.TenantId == Guid.Empty || command.ApiClientId == Guid.Empty)
        {
            return Result.Failure("TenantId and apiClientId are required.");
        }

        var deleted = await repository.DeleteAsync(command, cancellationToken);
        return deleted ? Result.Success() : Result.Failure("API client was not found.");
    }

    public async Task<Result<ApiClientDto>> UpdateAsync(
        UpdateApiClientCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationError = await ValidateUpdateAsync(command, cancellationToken);
        if (validationError is not null)
        {
            return Result<ApiClientDto>.Failure(validationError);
        }

        var updated = await repository.UpdateAsync(
            new UpdateApiClientCommand
            {
                ApiClientId = command.ApiClientId,
                TenantId = command.TenantId,
                StoreId = command.StoreId,
                Name = command.Name.Trim(),
                IsActive = command.IsActive,
                Permissions = NormalizePermissions(command.Permissions),
                RequestedByUserId = command.RequestedByUserId,
                IpAddress = command.IpAddress,
                UserAgent = command.UserAgent
            },
            cancellationToken);

        return updated is null
            ? Result<ApiClientDto>.Failure("API client was not found.")
            : Result<ApiClientDto>.Success(updated);
    }

    private async Task<string?> ValidateCreateAsync(
        CreateApiClientCommand command,
        CancellationToken cancellationToken)
    {
        if (command.TenantId == Guid.Empty)
        {
            return "TenantId is required.";
        }

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return "Name is required.";
        }

        if (command.Permissions.Count == 0)
        {
            return "At least one permission is required.";
        }

        var permissionsError = ValidatePermissions(command.Permissions);
        if (permissionsError is not null)
        {
            return permissionsError;
        }

        if (!await repository.TenantExistsAsync(command.TenantId, cancellationToken))
        {
            return "Tenant was not found.";
        }

        if (command.StoreId is not null
            && !await repository.StoreBelongsToTenantAsync(command.TenantId, command.StoreId.Value, cancellationToken))
        {
            return "Store was not found for this tenant.";
        }

        return null;
    }

    private async Task<string?> ValidateUpdateAsync(
        UpdateApiClientCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ApiClientId == Guid.Empty)
        {
            return "ApiClientId is required.";
        }

        if (command.TenantId == Guid.Empty)
        {
            return "TenantId is required.";
        }

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return "Name is required.";
        }

        var permissionsError = ValidatePermissions(command.Permissions);
        if (permissionsError is not null)
        {
            return permissionsError;
        }

        if (!await repository.TenantExistsAsync(command.TenantId, cancellationToken))
        {
            return "Tenant was not found.";
        }

        if (command.StoreId is not null
            && !await repository.StoreBelongsToTenantAsync(command.TenantId, command.StoreId.Value, cancellationToken))
        {
            return "Store was not found for this tenant.";
        }

        return null;
    }

    private static string? ValidatePermissions(IReadOnlyCollection<string> permissions)
    {
        if (permissions.Count == 0)
        {
            return "At least one permission is required.";
        }

        var invalidPermission = permissions
            .FirstOrDefault(permission => !ExternalApiPermissions.All.Contains(permission.Trim()));

        return invalidPermission is null ? null : $"Invalid API permission: {invalidPermission}.";
    }

    private static List<string> NormalizePermissions(IEnumerable<string> permissions) =>
        permissions
            .Select(permission => permission.Trim().ToLowerInvariant())
            .Where(permission => permission.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
