using Arca.Application.Catalog;
using Arca.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arca.Web.Controllers;

[ApiController]
[Route("api/admin/catalog/products/{productId:guid}/images")]
public sealed class AdminProductImagesController(
    ProductImageService productImageService,
    ICurrentUserService currentUserService) : ControllerBase
{
    [Authorize(Policy = KnownPermissions.ProductsView)]
    [HttpGet]
    public async Task<IActionResult> List(
        Guid productId,
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken)
    {
        var result = await productImageService.ListAsync(tenantId, productId, cancellationToken);
        if (result.IsFailure || result.Value is null)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(new { images = result.Value });
    }

    [Authorize(Policy = KnownPermissions.ProductsEdit)]
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(
        Guid productId,
        [FromForm] ProductImageUploadForm form,
        CancellationToken cancellationToken)
    {
        if (form.File is null)
        {
            return BadRequest(new { error = "Image file is required." });
        }

        await using var stream = form.File.OpenReadStream();
        var result = await productImageService.UploadAsync(
            new UploadProductImageCommand(
                form.TenantId,
                productId,
                form.ProductVariantId,
                form.File.FileName,
                form.File.ContentType,
                form.File.Length,
                stream,
                form.AltText,
                form.SortOrder,
                form.IsMain,
                currentUserService.UserId),
            cancellationToken);

        if (result.IsFailure || result.Value is null)
        {
            return BadRequest(new { error = result.Error });
        }

        return Created(
            $"/api/admin/catalog/products/{productId}/images/{result.Value.Image.Id}",
            result.Value);
    }

    [Authorize(Policy = KnownPermissions.ProductsEdit)]
    [HttpDelete("{imageId:guid}")]
    public async Task<IActionResult> Delete(
        Guid productId,
        Guid imageId,
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken)
    {
        var result = await productImageService.DeleteAsync(
            tenantId,
            productId,
            imageId,
            currentUserService.UserId,
            cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        return NoContent();
    }
}

public sealed class ProductImageUploadForm
{
    public Guid TenantId { get; init; }
    public Guid? ProductVariantId { get; init; }
    public IFormFile? File { get; init; }
    public string? AltText { get; init; }
    public int SortOrder { get; init; }
    public bool IsMain { get; init; }
}
