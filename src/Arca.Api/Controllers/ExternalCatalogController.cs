using Arca.Api.Middlewares;
using Arca.Application.Abstractions.ExternalApi;
using Arca.Application.ExternalApi;
using Microsoft.AspNetCore.Mvc;

namespace Arca.Api.Controllers;

[ApiController]
[Route("api/external/catalog")]
public sealed class ExternalCatalogController(
    IExternalCatalogRepository catalogRepository,
    IExternalApiClientContextAccessor clientContextAccessor) : ControllerBase
{
    [HttpGet("categories")]
    public async Task<IActionResult> Categories(CancellationToken cancellationToken)
    {
        var client = GetClientOrThrow();
        if (!client.HasPermission(ExternalApiPermissions.CatalogRead))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Missing catalog.read permission." });
        }

        var categories = await catalogRepository.ListCategoriesAsync(client, cancellationToken);
        return Ok(new { categories });
    }

    [HttpGet("products")]
    public async Task<IActionResult> Products(CancellationToken cancellationToken)
    {
        var client = GetClientOrThrow();
        if (!client.HasPermission(ExternalApiPermissions.CatalogRead))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Missing catalog.read permission." });
        }

        var products = await catalogRepository.ListProductsAsync(client, cancellationToken);
        return Ok(new { products });
    }

    [HttpGet("products/{id:guid}")]
    public async Task<IActionResult> Product(Guid id, CancellationToken cancellationToken)
    {
        var client = GetClientOrThrow();
        if (!client.HasPermission(ExternalApiPermissions.CatalogRead))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Missing catalog.read permission." });
        }

        var product = await catalogRepository.GetProductAsync(client, id, cancellationToken);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpGet("products/{id:guid}/variants")]
    public async Task<IActionResult> ProductVariants(Guid id, CancellationToken cancellationToken)
    {
        var client = GetClientOrThrow();
        if (!client.HasPermission(ExternalApiPermissions.CatalogRead))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Missing catalog.read permission." });
        }

        var variants = await catalogRepository.ListProductVariantsAsync(client, id, cancellationToken);
        return Ok(new { variants });
    }

    [HttpGet("variants/{id:guid}")]
    public async Task<IActionResult> Variant(Guid id, CancellationToken cancellationToken)
    {
        var client = GetClientOrThrow();
        if (!client.HasPermission(ExternalApiPermissions.CatalogRead))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Missing catalog.read permission." });
        }

        var variant = await catalogRepository.GetVariantAsync(client, id, cancellationToken);
        return variant is null ? NotFound() : Ok(variant);
    }

    [HttpGet("variants/{id:guid}/images")]
    public async Task<IActionResult> VariantImages(Guid id, CancellationToken cancellationToken)
    {
        var client = GetClientOrThrow();
        if (!client.HasPermission(ExternalApiPermissions.CatalogRead))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Missing catalog.read permission." });
        }

        var images = await catalogRepository.ListVariantImagesAsync(client, id, cancellationToken);
        return Ok(new { images });
    }

    private ExternalApiClientContext GetClientOrThrow() =>
        clientContextAccessor.Client ?? throw new InvalidOperationException("External API client context was not set.");
}
