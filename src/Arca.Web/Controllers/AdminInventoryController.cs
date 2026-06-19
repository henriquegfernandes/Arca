using Arca.Application.Inventory;
using Arca.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Arca.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/inventory")]
public sealed class AdminInventoryController(
    InventoryService inventoryService,
    ICurrentUserService currentUserService) : ControllerBase
{
    [Authorize(Policy = KnownPermissions.InventoryView)]
    [HttpGet("products")]
    public async Task<IActionResult> ListProducts(
        [FromQuery] Guid tenantId,
        [FromQuery] Guid storeId,
        [FromQuery] string? search,
        [FromQuery] Guid? categoryId,
        [FromQuery] string? status,
        [FromQuery] bool lowStockOnly = false,
        [FromQuery] bool outOfStockOnly = false,
        [FromQuery] Guid? stockLocationId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var result = await inventoryService.ListInventoryProductsAsync(
            tenantId,
            storeId,
            new InventoryProductFilters(search, categoryId, status, lowStockOnly, outOfStockOnly, stockLocationId),
            new Arca.Application.Common.PageRequest(page, pageSize, search),
            cancellationToken);

        if (result.IsFailure || result.Value is null)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(new
        {
            products = result.Value.Items,
            pagination = new
            {
                result.Value.Page,
                result.Value.PageSize,
                result.Value.TotalCount,
                result.Value.TotalPages
            }
        });
    }

    [Authorize(Policy = KnownPermissions.InventoryView)]
    [HttpGet("products/{productId:guid}")]
    public async Task<IActionResult> GetProduct(
        Guid productId,
        [FromQuery] Guid tenantId,
        [FromQuery] Guid storeId,
        [FromQuery] Guid? stockLocationId,
        CancellationToken cancellationToken)
    {
        var result = await inventoryService.GetInventoryProductDetailsAsync(
            tenantId,
            storeId,
            productId,
            stockLocationId,
            cancellationToken);

        if (result.IsFailure || result.Value is null)
        {
            return NotFound(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    [Authorize(Policy = KnownPermissions.InventoryView)]
    [HttpGet("balance")]
    public async Task<IActionResult> GetBalance(
        [FromQuery] Guid tenantId,
        [FromQuery] Guid storeId,
        [FromQuery] Guid stockLocationId,
        [FromQuery] Guid productVariantId,
        CancellationToken cancellationToken)
    {
        var result = await inventoryService.GetBalanceAsync(
            tenantId,
            storeId,
            stockLocationId,
            productVariantId,
            cancellationToken);

        if (result.IsFailure || result.Value is null)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    [Authorize(Policy = KnownPermissions.InventoryView)]
    [HttpGet("stock-locations")]
    public async Task<IActionResult> ListStockLocations(
        [FromQuery] Guid tenantId,
        [FromQuery] Guid storeId,
        CancellationToken cancellationToken)
    {
        var result = await inventoryService.ListStockLocationsAsync(tenantId, storeId, cancellationToken);
        if (result.IsFailure || result.Value is null)
            return BadRequest(new { error = result.Error });

        return Ok(new { stockLocations = result.Value });
    }

    [Authorize(Policy = KnownPermissions.InventoryView)]
    [HttpGet("movements")]
    public async Task<IActionResult> ListMovements(
        [FromQuery] Guid tenantId,
        [FromQuery] Guid storeId,
        [FromQuery] Guid? productVariantId,
        [FromQuery] int limit,
        CancellationToken cancellationToken)
    {
        var result = await inventoryService.ListMovementsAsync(
            tenantId,
            storeId,
            productVariantId,
            limit,
            cancellationToken);

        if (result.IsFailure || result.Value is null)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(new { movements = result.Value });
    }

    [Authorize(Policy = KnownPermissions.InventoryEntry)]
    [HttpPost("entries")]
    public async Task<IActionResult> RegisterEntry(
        [FromBody] RegisterStockEntryCommand request,
        CancellationToken cancellationToken)
    {
        var command = new RegisterStockEntryCommand
        {
            TenantId = request.TenantId,
            StoreId = request.StoreId,
            StockLocationId = request.StockLocationId,
            ProductVariantId = request.ProductVariantId,
            Quantity = request.Quantity,
            UnitCost = request.UnitCost,
            Reason = request.Reason,
            Notes = request.Notes,
            BatchNumber = request.BatchNumber,
            ExpirationDate = request.ExpirationDate,
            RequestedByUserId = currentUserService.UserId
        };

        var result = await inventoryService.RegisterEntryAsync(command, cancellationToken);
        if (result.IsFailure || result.Value is null)
        {
            return BadRequest(new { error = result.Error });
        }

        return Created($"/api/admin/inventory/movements/{result.Value.Movement.Id}", result.Value);
    }

    [Authorize(Policy = KnownPermissions.InventoryExit)]
    [HttpPost("exits")]
    public async Task<IActionResult> RegisterExit(
        [FromBody] RegisterStockExitCommand request,
        CancellationToken cancellationToken)
    {
        var command = new RegisterStockExitCommand
        {
            TenantId = request.TenantId,
            StoreId = request.StoreId,
            StockLocationId = request.StockLocationId,
            ProductVariantId = request.ProductVariantId,
            Quantity = request.Quantity,
            MovementType = request.MovementType,
            Reason = request.Reason,
            Notes = request.Notes,
            RequestedByUserId = currentUserService.UserId
        };

        var result = await inventoryService.RegisterExitAsync(command, cancellationToken);
        if (result.IsFailure || result.Value is null)
        {
            return BadRequest(new { error = result.Error });
        }

        return Created($"/api/admin/inventory/movements/{result.Value.Movement.Id}", result.Value);
    }

    [Authorize(Policy = KnownPermissions.InventoryAdjust)]
    [HttpPost("adjustments")]
    public async Task<IActionResult> Adjust(
        [FromBody] AdjustStockCommand request,
        CancellationToken cancellationToken)
    {
        var command = new AdjustStockCommand
        {
            TenantId = request.TenantId,
            StoreId = request.StoreId,
            StockLocationId = request.StockLocationId,
            ProductVariantId = request.ProductVariantId,
            NewQuantity = request.NewQuantity,
            MinimumStock = request.MinimumStock,
            Reason = request.Reason,
            Notes = request.Notes,
            RequestedByUserId = currentUserService.UserId
        };

        var result = await inventoryService.AdjustAsync(command, cancellationToken);
        if (result.IsFailure || result.Value is null)
        {
            return BadRequest(new { error = result.Error });
        }

        return Created($"/api/admin/inventory/movements/{result.Value.Movement.Id}", result.Value);
    }

    [Authorize]
    [HttpPost("movements")]
    public async Task<IActionResult> RegisterMovement(
        [FromBody] StockMovementRequest request,
        CancellationToken cancellationToken)
    {
        var policy = request.Type.Equals("Entry", StringComparison.OrdinalIgnoreCase)
            || request.Type.Equals("Purchase", StringComparison.OrdinalIgnoreCase)
            ? KnownPermissions.InventoryEntry
            : request.Type.Equals("Adjust", StringComparison.OrdinalIgnoreCase)
                || request.Type.Equals("Adjustment", StringComparison.OrdinalIgnoreCase)
                    ? KnownPermissions.InventoryAdjust
                    : KnownPermissions.InventoryExit;

        var authorizationResult = await HttpContext.RequestServices
            .GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(User, policy);

        if (!authorizationResult.Succeeded)
        {
            return Forbid();
        }

        var command = new StockMovementRequest
        {
            Type = request.Type,
            TenantId = request.TenantId,
            StoreId = request.StoreId,
            StockLocationId = request.StockLocationId,
            Items = request.Items,
            Reason = request.Reason,
            Notes = request.Notes,
            RequestedByUserId = currentUserService.UserId
        };

        var result = await inventoryService.RegisterMovementAsync(command, cancellationToken);
        if (result.IsFailure || result.Value is null)
        {
            return BadRequest(new { error = result.Error });
        }

        return Created("/api/admin/inventory/movements", new { movements = result.Value });
    }
}
