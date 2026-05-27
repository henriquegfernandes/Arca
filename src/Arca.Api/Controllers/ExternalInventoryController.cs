using Arca.Api.Middlewares;
using Arca.Application.Abstractions.ExternalApi;
using Arca.Application.ExternalApi;
using Microsoft.AspNetCore.Mvc;

namespace Arca.Api.Controllers;

[ApiController]
[Route("api/external/inventory")]
public sealed class ExternalInventoryController(
    IExternalCatalogRepository catalogRepository,
    IExternalApiClientContextAccessor clientContextAccessor) : ControllerBase
{
    [HttpGet("availability")]
    public async Task<IActionResult> Availability(
        [FromQuery] Guid variantId,
        CancellationToken cancellationToken)
    {
        var client = clientContextAccessor.Client ?? throw new InvalidOperationException("External API client context was not set.");
        if (!client.HasPermission(ExternalApiPermissions.InventoryRead))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Missing inventory.read permission." });
        }

        var availability = await catalogRepository.GetAvailabilityAsync(client, variantId, cancellationToken);
        return availability is null ? NotFound() : Ok(availability);
    }
}
