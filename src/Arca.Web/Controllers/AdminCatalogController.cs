using Arca.Application.Catalog;
using Arca.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arca.Web.Controllers;

[ApiController]
[Route("api/admin/catalog")]
public sealed class AdminCatalogController(
    ProductCatalogService productCatalogService,
    ICurrentUserService currentUserService) : ControllerBase
{
    [Authorize(Policy = KnownPermissions.ProductsView)]
    [HttpPost("variants/preview")]
    public async Task<IActionResult> PreviewVariants(
        [FromBody] PreviewProductVariantsCommand request,
        CancellationToken cancellationToken)
    {
        var result = await productCatalogService.PreviewVariantsAsync(request, cancellationToken);
        if (result.IsFailure || result.Value is null)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    [Authorize(Policy = KnownPermissions.ProductsCreate)]
    [HttpPost("products")]
    public async Task<IActionResult> CreateProduct(
        [FromBody] CreateProductCommand request,
        CancellationToken cancellationToken)
    {
        var command = new CreateProductCommand
        {
            TenantId = request.TenantId,
            CategoryId = request.CategoryId,
            ProductTypeId = request.ProductTypeId,
            ProductName = request.ProductName,
            Slug = request.Slug,
            Description = request.Description,
            BaseSku = request.BaseSku,
            Barcode = request.Barcode,
            Brand = request.Brand,
            Status = request.Status,
            DefaultSalePrice = request.DefaultSalePrice,
            DefaultCostPrice = request.DefaultCostPrice,
            VariantAttributes = request.VariantAttributes,
            RequestedByUserId = currentUserService.UserId
        };

        var result = await productCatalogService.CreateProductAsync(command, cancellationToken);
        if (result.IsFailure || result.Value is null)
        {
            return BadRequest(new { error = result.Error });
        }

        return Created($"/api/admin/catalog/products/{result.Value.ProductId}", result.Value);
    }
}
