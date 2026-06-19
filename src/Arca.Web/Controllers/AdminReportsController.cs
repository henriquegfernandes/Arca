using Arca.Application.Export;
using Arca.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arca.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/reports")]
public sealed class AdminReportsController(CsvExportService csvExportService) : ControllerBase
{
    [Authorize(Policy = KnownPermissions.ReportsView)]
    [HttpGet("products.csv")]
    public async Task<IActionResult> ExportProducts(
        [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        var result = await csvExportService.ExportProductsAsync(tenantId, cancellationToken);
        if (result.IsFailure || result.Value is null)
            return BadRequest(new { error = result.Error });

        return File(result.Value, "text/csv", $"products-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [Authorize(Policy = KnownPermissions.ReportsView)]
    [HttpGet("inventory.csv")]
    public async Task<IActionResult> ExportInventory(
        [FromQuery] Guid tenantId, [FromQuery] Guid storeId, CancellationToken cancellationToken)
    {
        var result = await csvExportService.ExportInventoryAsync(tenantId, storeId, cancellationToken);
        if (result.IsFailure || result.Value is null)
            return BadRequest(new { error = result.Error });

        return File(result.Value, "text/csv", $"inventory-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [Authorize(Policy = KnownPermissions.ReportsView)]
    [HttpGet("movements.csv")]
    public async Task<IActionResult> ExportMovements(
        [FromQuery] Guid tenantId, [FromQuery] Guid storeId, CancellationToken cancellationToken)
    {
        var result = await csvExportService.ExportMovementsAsync(tenantId, storeId, cancellationToken);
        if (result.IsFailure || result.Value is null)
            return BadRequest(new { error = result.Error });

        return File(result.Value, "text/csv", $"movements-{DateTime.UtcNow:yyyyMMdd}.csv");
    }
}
