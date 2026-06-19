using Arca.Application.Abstractions.Inventory;
using Arca.Application.Common;

namespace Arca.Application.Inventory;

public sealed class InventoryService(IInventoryRepository repository)
{
    private static readonly HashSet<string> ExitMovementTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Sale",
        "TransferOut",
        "Loss",
        "Consumption"
    };

    public async Task<Result<InventoryOperationResult>> RegisterEntryAsync(
        RegisterStockEntryCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationError = await ValidateBaseAsync(
            command.TenantId,
            command.StoreId,
            command.StockLocationId,
            command.ProductVariantId,
            command.Quantity,
            cancellationToken);

        if (validationError is not null)
        {
            return Result<InventoryOperationResult>.Failure(validationError);
        }

        var result = await repository.ApplyAsync(
            new InventoryOperationData(
                command.TenantId,
                command.StoreId,
                command.StockLocationId,
                command.ProductVariantId,
                "Purchase",
                command.Quantity,
                null,
                null,
                command.UnitCost,
                TrimToNull(command.Reason),
                TrimToNull(command.Notes),
                TrimToNull(command.BatchNumber),
                command.ExpirationDate,
                command.RequestedByUserId),
            cancellationToken);

        return Result<InventoryOperationResult>.Success(result);
    }

    public async Task<Result<InventoryOperationResult>> RegisterExitAsync(
        RegisterStockExitCommand command,
        CancellationToken cancellationToken = default)
    {
        var movementType = NormalizeMovementType(command.MovementType);
        if (!ExitMovementTypes.Contains(movementType))
        {
            return Result<InventoryOperationResult>.Failure("MovementType must be Sale, TransferOut, Loss or Consumption.");
        }

        var validationError = await ValidateBaseAsync(
            command.TenantId,
            command.StoreId,
            command.StockLocationId,
            command.ProductVariantId,
            command.Quantity,
            cancellationToken);

        if (validationError is not null)
        {
            return Result<InventoryOperationResult>.Failure(validationError);
        }

        var balance = await repository.GetBalanceAsync(
            command.TenantId,
            command.StoreId,
            command.StockLocationId,
            command.ProductVariantId,
            cancellationToken);

        if (balance is null || balance.AvailableQuantity < command.Quantity)
        {
            return Result<InventoryOperationResult>.Failure("Insufficient available stock.");
        }

        var result = await repository.ApplyAsync(
            new InventoryOperationData(
                command.TenantId,
                command.StoreId,
                command.StockLocationId,
                command.ProductVariantId,
                movementType,
                -command.Quantity,
                null,
                null,
                null,
                TrimToNull(command.Reason),
                TrimToNull(command.Notes),
                null,
                null,
                command.RequestedByUserId),
            cancellationToken);

        return Result<InventoryOperationResult>.Success(result);
    }

    public async Task<Result<InventoryOperationResult>> AdjustAsync(
        AdjustStockCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.NewQuantity < 0)
        {
            return Result<InventoryOperationResult>.Failure("NewQuantity cannot be negative.");
        }

        if (command.MinimumStock is < 0)
        {
            return Result<InventoryOperationResult>.Failure("MinimumStock cannot be negative.");
        }

        var validationError = await ValidateBaseAsync(
            command.TenantId,
            command.StoreId,
            command.StockLocationId,
            command.ProductVariantId,
            1,
            cancellationToken);

        if (validationError is not null)
        {
            return Result<InventoryOperationResult>.Failure(validationError);
        }

        var current = await repository.GetBalanceAsync(
            command.TenantId,
            command.StoreId,
            command.StockLocationId,
            command.ProductVariantId,
            cancellationToken);

        var currentQuantity = current?.Quantity ?? 0;
        var quantityDelta = command.NewQuantity - currentQuantity;
        if (quantityDelta == 0 && command.MinimumStock is null)
        {
            return Result<InventoryOperationResult>.Failure("No inventory change was requested.");
        }

        var result = await repository.ApplyAsync(
            new InventoryOperationData(
                command.TenantId,
                command.StoreId,
                command.StockLocationId,
                command.ProductVariantId,
                "Adjustment",
                quantityDelta == 0 ? 1 : quantityDelta,
                command.NewQuantity,
                command.MinimumStock,
                null,
                TrimToNull(command.Reason),
                TrimToNull(command.Notes),
                null,
                null,
                command.RequestedByUserId),
            cancellationToken);

        return Result<InventoryOperationResult>.Success(result);
    }

    public async Task<Result<InventoryBalanceDto>> GetBalanceAsync(
        Guid tenantId,
        Guid storeId,
        Guid stockLocationId,
        Guid productVariantId,
        CancellationToken cancellationToken = default)
    {
        var validationError = await ValidateLocationAndVariantAsync(tenantId, storeId, stockLocationId, productVariantId, cancellationToken);
        if (validationError is not null)
        {
            return Result<InventoryBalanceDto>.Failure(validationError);
        }

        var balance = await repository.GetBalanceAsync(tenantId, storeId, stockLocationId, productVariantId, cancellationToken);
        return Result<InventoryBalanceDto>.Success(balance ?? new InventoryBalanceDto(
            Guid.Empty,
            stockLocationId,
            productVariantId,
            0,
            0,
            0,
            0,
            null));
    }

    public async Task<Result<PagedResult<InventoryProductSummaryDto>>> ListInventoryProductsAsync(
        Guid tenantId,
        Guid storeId,
        InventoryProductFilters filters,
        PageRequest pageRequest,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || storeId == Guid.Empty)
        {
            return Result<PagedResult<InventoryProductSummaryDto>>.Failure("TenantId and StoreId are required.");
        }

        if (filters.StockLocationId.HasValue
            && !await repository.StockLocationBelongsToStoreAsync(tenantId, storeId, filters.StockLocationId.Value, cancellationToken))
        {
            return Result<PagedResult<InventoryProductSummaryDto>>.Failure("Stock location was not found for this tenant/store.");
        }

        var products = await repository.ListInventoryProductsAsync(tenantId, storeId, filters, pageRequest, cancellationToken);
        return Result<PagedResult<InventoryProductSummaryDto>>.Success(products);
    }

    public async Task<Result<InventoryProductDetailsDto>> GetInventoryProductDetailsAsync(
        Guid tenantId,
        Guid storeId,
        Guid productId,
        Guid? stockLocationId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || storeId == Guid.Empty || productId == Guid.Empty)
        {
            return Result<InventoryProductDetailsDto>.Failure("TenantId, StoreId and ProductId are required.");
        }

        if (stockLocationId.HasValue
            && !await repository.StockLocationBelongsToStoreAsync(tenantId, storeId, stockLocationId.Value, cancellationToken))
        {
            return Result<InventoryProductDetailsDto>.Failure("Stock location was not found for this tenant/store.");
        }

        var product = await repository.GetInventoryProductDetailsAsync(tenantId, storeId, productId, stockLocationId, cancellationToken);
        return product is null
            ? Result<InventoryProductDetailsDto>.Failure("Product was not found.")
            : Result<InventoryProductDetailsDto>.Success(product);
    }

    public async Task<Result<IReadOnlyCollection<InventoryOperationResult>>> RegisterMovementAsync(
        StockMovementRequest command,
        CancellationToken cancellationToken = default)
    {
        if (command.Items.Count == 0)
        {
            return Result<IReadOnlyCollection<InventoryOperationResult>>.Failure("At least one movement item is required.");
        }

        var results = new List<InventoryOperationResult>();
        var type = command.Type.Trim();
        foreach (var item in command.Items)
        {
            var itemStockLocationId = item.StockLocationId ?? command.StockLocationId;
            Result<InventoryOperationResult> result;
            if (type.Equals("Entry", StringComparison.OrdinalIgnoreCase) || type.Equals("Purchase", StringComparison.OrdinalIgnoreCase))
            {
                result = await RegisterEntryAsync(new RegisterStockEntryCommand
                {
                    TenantId = command.TenantId,
                    StoreId = command.StoreId,
                    StockLocationId = itemStockLocationId,
                    ProductVariantId = item.ProductVariantId,
                    Quantity = item.Quantity,
                    UnitCost = item.UnitCost,
                    Reason = command.Reason,
                    Notes = command.Notes,
                    RequestedByUserId = command.RequestedByUserId
                }, cancellationToken);
            }
            else if (type.Equals("Exit", StringComparison.OrdinalIgnoreCase) || ExitMovementTypes.Contains(type))
            {
                result = await RegisterExitAsync(new RegisterStockExitCommand
                {
                    TenantId = command.TenantId,
                    StoreId = command.StoreId,
                    StockLocationId = itemStockLocationId,
                    ProductVariantId = item.ProductVariantId,
                    Quantity = item.Quantity,
                    MovementType = ExitMovementTypes.Contains(type) ? type : "Sale",
                    Reason = command.Reason,
                    Notes = command.Notes,
                    RequestedByUserId = command.RequestedByUserId
                }, cancellationToken);
            }
            else if (type.Equals("Adjust", StringComparison.OrdinalIgnoreCase) || type.Equals("Adjustment", StringComparison.OrdinalIgnoreCase))
            {
                result = await AdjustAsync(new AdjustStockCommand
                {
                    TenantId = command.TenantId,
                    StoreId = command.StoreId,
                    StockLocationId = itemStockLocationId,
                    ProductVariantId = item.ProductVariantId,
                    NewQuantity = item.Quantity,
                    Reason = command.Reason,
                    Notes = command.Notes,
                    RequestedByUserId = command.RequestedByUserId
                }, cancellationToken);
            }
            else
            {
                return Result<IReadOnlyCollection<InventoryOperationResult>>.Failure("Type must be Entry, Exit or Adjustment.");
            }

            if (result.IsFailure || result.Value is null)
            {
                return Result<IReadOnlyCollection<InventoryOperationResult>>.Failure(result.Error ?? "Inventory movement failed.");
            }

            results.Add(result.Value);
        }

        return Result<IReadOnlyCollection<InventoryOperationResult>>.Success(results);
    }

    public async Task<Result<IReadOnlyCollection<StockLocationDto>>> ListStockLocationsAsync(
        Guid tenantId,
        Guid storeId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || storeId == Guid.Empty)
            return Result<IReadOnlyCollection<StockLocationDto>>.Failure("TenantId and StoreId are required.");

        var locations = await repository.ListStockLocationsAsync(tenantId, storeId, cancellationToken);
        return Result<IReadOnlyCollection<StockLocationDto>>.Success(locations);
    }

    public async Task<Result<IReadOnlyCollection<StockMovementDto>>> ListMovementsAsync(
        Guid tenantId,
        Guid storeId,
        Guid? productVariantId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || storeId == Guid.Empty)
        {
            return Result<IReadOnlyCollection<StockMovementDto>>.Failure("TenantId and StoreId are required.");
        }

        var normalizedLimit = Math.Clamp(limit <= 0 ? 50 : limit, 1, 200);
        var movements = await repository.ListMovementsAsync(tenantId, storeId, productVariantId, normalizedLimit, cancellationToken);

        return Result<IReadOnlyCollection<StockMovementDto>>.Success(movements);
    }

    private async Task<string?> ValidateBaseAsync(
        Guid tenantId,
        Guid storeId,
        Guid stockLocationId,
        Guid productVariantId,
        int quantity,
        CancellationToken cancellationToken)
    {
        if (quantity <= 0)
        {
            return "Quantity must be greater than zero.";
        }

        return await ValidateLocationAndVariantAsync(tenantId, storeId, stockLocationId, productVariantId, cancellationToken);
    }

    private async Task<string?> ValidateLocationAndVariantAsync(
        Guid tenantId,
        Guid storeId,
        Guid stockLocationId,
        Guid productVariantId,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || storeId == Guid.Empty || stockLocationId == Guid.Empty || productVariantId == Guid.Empty)
        {
            return "TenantId, StoreId, StockLocationId and ProductVariantId are required.";
        }

        if (!await repository.StockLocationBelongsToStoreAsync(tenantId, storeId, stockLocationId, cancellationToken))
        {
            return "Stock location was not found for this tenant/store.";
        }

        if (!await repository.ProductVariantBelongsToTenantAsync(tenantId, productVariantId, cancellationToken))
        {
            return "Product variant was not found for this tenant.";
        }

        return null;
    }

    private static string NormalizeMovementType(string value)
    {
        var normalized = value.Trim();
        return ExitMovementTypes.FirstOrDefault(type => type.Equals(normalized, StringComparison.OrdinalIgnoreCase)) ?? normalized;
    }

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
