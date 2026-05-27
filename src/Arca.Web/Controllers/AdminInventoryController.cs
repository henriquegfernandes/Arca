using Arca.Application.Inventory;
using Arca.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arca.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/inventory")]
public sealed class AdminInventoryController(
    InventoryService inventoryService,
    ICurrentUserService currentUserService) : ControllerBase
{
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
}
