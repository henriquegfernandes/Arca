using Arca.Application.Catalog;
using Arca.Application.Common;
using Arca.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arca.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/catalog")]
public sealed class AdminCatalogController(
    ProductCatalogService productCatalogService,
    CatalogManagementService catalogManagementService,
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
            return BadRequest(new { error = result.Error });

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
            return BadRequest(new { error = result.Error });

        return Created($"/api/admin/catalog/products/{result.Value.ProductId}", result.Value);
    }

    [Authorize(Policy = KnownPermissions.CategoriesManage)]
    [HttpGet("categories")]
    public async Task<IActionResult> ListCategories(
        [FromQuery] Guid tenantId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await catalogManagementService.ListCategoriesAsync(
            tenantId, new PageRequest(page, pageSize, search), cancellationToken);
        if (result.IsFailure || result.Value is null)
            return BadRequest(new { error = result.Error });

        return Ok(PagedResponse("categories", result.Value));
    }

    [Authorize(Policy = KnownPermissions.CategoriesManage)]
    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory(
        [FromBody] CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        var command = new CreateCategoryCommand
        {
            TenantId = request.TenantId,
            ParentCategoryId = request.ParentCategoryId,
            Name = request.Name,
            Description = request.Description,
            SortOrder = request.SortOrder,
            RequestedByUserId = currentUserService.UserId
        };

        var result = await catalogManagementService.CreateCategoryAsync(command, cancellationToken);
        if (result.IsFailure || result.Value is null)
            return BadRequest(new { error = result.Error });

        return Created($"/api/admin/catalog/categories/{result.Value.Id}", result.Value);
    }

    [Authorize(Policy = KnownPermissions.CategoriesManage)]
    [HttpPut("categories/{categoryId:guid}")]
    public async Task<IActionResult> UpdateCategory(
        Guid categoryId, [FromBody] UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        var command = new UpdateCategoryCommand
        {
            TenantId = request.TenantId,
            CategoryId = categoryId,
            ParentCategoryId = request.ParentCategoryId,
            Name = request.Name,
            Description = request.Description,
            SortOrder = request.SortOrder,
            RequestedByUserId = currentUserService.UserId
        };

        var result = await catalogManagementService.UpdateCategoryAsync(command, cancellationToken);
        if (result.IsFailure || result.Value is null)
            return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }

    [Authorize(Policy = KnownPermissions.CategoriesManage)]
    [HttpDelete("categories/{categoryId:guid}")]
    public async Task<IActionResult> DisableCategory(
        Guid categoryId, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        var result = await catalogManagementService.DisableCategoryAsync(tenantId, categoryId, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        return NoContent();
    }

    [Authorize(Policy = KnownPermissions.CategoriesManage)]
    [HttpPost("categories/{categoryId:guid}/activate")]
    public async Task<IActionResult> ActivateCategory(
        Guid categoryId, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        var result = await catalogManagementService.ActivateCategoryAsync(tenantId, categoryId, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        return NoContent();
    }

    [Authorize(Policy = KnownPermissions.CategoriesManage)]
    [HttpDelete("categories/{categoryId:guid}/delete")]
    public async Task<IActionResult> DeleteCategory(
        Guid categoryId, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        var result = await catalogManagementService.DeleteCategoryAsync(
            tenantId, categoryId, currentUserService.UserId, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        return NoContent();
    }

    [Authorize(Policy = KnownPermissions.ProductTypesManage)]
    [HttpGet("product-types")]
    public async Task<IActionResult> ListProductTypes(
        [FromQuery] Guid tenantId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await catalogManagementService.ListProductTypesAsync(
            tenantId, new PageRequest(page, pageSize, search), cancellationToken);
        if (result.IsFailure || result.Value is null)
            return BadRequest(new { error = result.Error });

        return Ok(PagedResponse("productTypes", result.Value));
    }

    [Authorize(Policy = KnownPermissions.ProductTypesManage)]
    [HttpPost("product-types")]
    public async Task<IActionResult> CreateProductType(
        [FromBody] CreateProductTypeCommand request, CancellationToken cancellationToken)
    {
        var command = new CreateProductTypeCommand
        {
            TenantId = request.TenantId,
            Name = request.Name,
            Description = request.Description,
            RequestedByUserId = currentUserService.UserId
        };

        var result = await catalogManagementService.CreateProductTypeAsync(command, cancellationToken);
        if (result.IsFailure || result.Value is null)
            return BadRequest(new { error = result.Error });

        return Created($"/api/admin/catalog/product-types/{result.Value.Id}", result.Value);
    }

    [Authorize(Policy = KnownPermissions.ProductTypesManage)]
    [HttpPut("product-types/{productTypeId:guid}")]
    public async Task<IActionResult> UpdateProductType(
        Guid productTypeId, [FromBody] UpdateProductTypeCommand request, CancellationToken cancellationToken)
    {
        var command = new UpdateProductTypeCommand
        {
            TenantId = request.TenantId,
            ProductTypeId = productTypeId,
            Name = request.Name,
            Description = request.Description,
            RequestedByUserId = currentUserService.UserId
        };

        var result = await catalogManagementService.UpdateProductTypeAsync(command, cancellationToken);
        if (result.IsFailure || result.Value is null)
            return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }

    [Authorize(Policy = KnownPermissions.ProductTypesManage)]
    [HttpDelete("product-types/{productTypeId:guid}")]
    public async Task<IActionResult> DisableProductType(
        Guid productTypeId, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        var result = await catalogManagementService.DisableProductTypeAsync(tenantId, productTypeId, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        return NoContent();
    }

    [Authorize(Policy = KnownPermissions.ProductTypesManage)]
    [HttpPost("product-types/{productTypeId:guid}/activate")]
    public async Task<IActionResult> ActivateProductType(
        Guid productTypeId, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        var result = await catalogManagementService.ActivateProductTypeAsync(tenantId, productTypeId, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        return NoContent();
    }

    [Authorize(Policy = KnownPermissions.ProductTypesManage)]
    [HttpDelete("product-types/{productTypeId:guid}/delete")]
    public async Task<IActionResult> DeleteProductType(
        Guid productTypeId, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        var result = await catalogManagementService.DeleteProductTypeAsync(
            tenantId, productTypeId, currentUserService.UserId, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        return NoContent();
    }

    [Authorize(Policy = KnownPermissions.ProductsView)]
    [HttpGet("product-types/{productTypeId:guid}/attributes")]
    public async Task<IActionResult> ListProductTypeAttributes(
        Guid productTypeId,
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var result = await catalogManagementService.ListProductTypeAttributesAsync(
            tenantId, productTypeId, cancellationToken);
        if (result.IsFailure || result.Value is null)
            return BadRequest(new { error = result.Error });

        return Ok(new { attributes = result.Value });
    }

    [Authorize(Policy = KnownPermissions.AttributesManage)]
    [HttpGet("attributes")]
    public async Task<IActionResult> ListAttributes(
        [FromQuery] Guid tenantId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await catalogManagementService.ListAttributesAsync(
            tenantId, new PageRequest(page, pageSize, search), cancellationToken);
        if (result.IsFailure || result.Value is null)
            return BadRequest(new { error = result.Error });

        return Ok(PagedResponse("attributes", result.Value));
    }

    [Authorize(Policy = KnownPermissions.AttributesManage)]
    [HttpPost("attributes")]
    public async Task<IActionResult> CreateAttribute(
        [FromBody] CreateAttributeCommand request, CancellationToken cancellationToken)
    {
        var command = new CreateAttributeCommand
        {
            TenantId = request.TenantId,
            Name = request.Name,
            Code = request.Code,
            Description = request.Description,
            AttributeType = request.AttributeType,
            IsVariantAttribute = request.IsVariantAttribute,
            IsRequired = request.IsRequired,
            SortOrder = request.SortOrder,
            RequestedByUserId = currentUserService.UserId
        };

        var result = await catalogManagementService.CreateAttributeAsync(command, cancellationToken);
        if (result.IsFailure || result.Value is null)
            return BadRequest(new { error = result.Error });

        return Created($"/api/admin/catalog/attributes/{result.Value.Id}", result.Value);
    }

    [Authorize(Policy = KnownPermissions.AttributesManage)]
    [HttpPut("attributes/{attributeId:guid}")]
    public async Task<IActionResult> UpdateAttribute(
        Guid attributeId, [FromBody] UpdateAttributeCommand request, CancellationToken cancellationToken)
    {
        var command = new UpdateAttributeCommand
        {
            TenantId = request.TenantId,
            AttributeId = attributeId,
            Name = request.Name,
            Code = request.Code,
            Description = request.Description,
            AttributeType = request.AttributeType,
            IsVariantAttribute = request.IsVariantAttribute,
            IsRequired = request.IsRequired,
            SortOrder = request.SortOrder,
            RequestedByUserId = currentUserService.UserId
        };

        var result = await catalogManagementService.UpdateAttributeAsync(command, cancellationToken);
        if (result.IsFailure || result.Value is null)
            return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }

    [Authorize(Policy = KnownPermissions.AttributesManage)]
    [HttpDelete("attributes/{attributeId:guid}")]
    public async Task<IActionResult> DisableAttribute(
        Guid attributeId, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        var result = await catalogManagementService.DisableAttributeAsync(tenantId, attributeId, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        return NoContent();
    }

    [Authorize(Policy = KnownPermissions.AttributesManage)]
    [HttpPost("attributes/{attributeId:guid}/activate")]
    public async Task<IActionResult> ActivateAttribute(
        Guid attributeId, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        var result = await catalogManagementService.ActivateAttributeAsync(tenantId, attributeId, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        return NoContent();
    }

    [Authorize(Policy = KnownPermissions.AttributesManage)]
    [HttpDelete("attributes/{attributeId:guid}/delete")]
    public async Task<IActionResult> DeleteAttribute(
        Guid attributeId, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        var result = await catalogManagementService.DeleteAttributeAsync(
            tenantId, attributeId, currentUserService.UserId, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        return NoContent();
    }

    [Authorize(Policy = KnownPermissions.AttributesManage)]
    [HttpGet("attributes/{attributeId:guid}/values")]
    public async Task<IActionResult> ListAttributeValues(
        Guid attributeId,
        [FromQuery] Guid tenantId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await catalogManagementService.ListAttributeValuesAsync(
            tenantId, attributeId, new PageRequest(page, pageSize, search), cancellationToken);
        if (result.IsFailure || result.Value is null)
            return BadRequest(new { error = result.Error });

        return Ok(PagedResponse("values", result.Value));
    }

    [Authorize(Policy = KnownPermissions.AttributesManage)]
    [HttpPost("attributes/{attributeId:guid}/values")]
    public async Task<IActionResult> CreateAttributeValue(
        Guid attributeId, [FromBody] CreateAttributeValueCommand request, CancellationToken cancellationToken)
    {
        var command = new CreateAttributeValueCommand
        {
            TenantId = request.TenantId,
            ProductAttributeId = attributeId,
            Name = request.Name,
            Code = request.Code,
            Value = request.Value,
            HexCode = request.HexCode,
            SortOrder = request.SortOrder,
            RequestedByUserId = currentUserService.UserId
        };

        var result = await catalogManagementService.CreateAttributeValueAsync(command, cancellationToken);
        if (result.IsFailure || result.Value is null)
            return BadRequest(new { error = result.Error });

        return Created($"/api/admin/catalog/attributes/{attributeId}/values/{result.Value.Id}", result.Value);
    }

    [Authorize(Policy = KnownPermissions.AttributesManage)]
    [HttpPut("attributes/{attributeId:guid}/values/{valueId:guid}")]
    public async Task<IActionResult> UpdateAttributeValue(
        Guid attributeId, Guid valueId, [FromBody] UpdateAttributeValueCommand request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateAttributeValueCommand
        {
            TenantId = request.TenantId,
            ProductAttributeId = attributeId,
            ValueId = valueId,
            Name = request.Name,
            Code = request.Code,
            Value = request.Value,
            HexCode = request.HexCode,
            SortOrder = request.SortOrder,
            RequestedByUserId = currentUserService.UserId
        };

        var result = await catalogManagementService.UpdateAttributeValueAsync(command, cancellationToken);
        if (result.IsFailure || result.Value is null)
            return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }

    [Authorize(Policy = KnownPermissions.AttributesManage)]
    [HttpDelete("attributes/{attributeId:guid}/values/{valueId:guid}")]
    public async Task<IActionResult> DisableAttributeValue(
        Guid attributeId, Guid valueId, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        var result = await catalogManagementService.DisableAttributeValueAsync(tenantId, attributeId, valueId, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        return NoContent();
    }

    [Authorize(Policy = KnownPermissions.AttributesManage)]
    [HttpPost("attributes/{attributeId:guid}/values/{valueId:guid}/activate")]
    public async Task<IActionResult> ActivateAttributeValue(
        Guid attributeId, Guid valueId, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        var result = await catalogManagementService.ActivateAttributeValueAsync(
            tenantId, attributeId, valueId, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        return NoContent();
    }

    [Authorize(Policy = KnownPermissions.AttributesManage)]
    [HttpDelete("attributes/{attributeId:guid}/values/{valueId:guid}/delete")]
    public async Task<IActionResult> DeleteAttributeValue(
        Guid attributeId, Guid valueId, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        var result = await catalogManagementService.DeleteAttributeValueAsync(
            tenantId, attributeId, valueId, currentUserService.UserId, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        return NoContent();
    }

    [Authorize(Policy = KnownPermissions.ProductsView)]
    [HttpGet("products")]
    public async Task<IActionResult> ListProducts(
        [FromQuery] Guid tenantId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await catalogManagementService.ListProductsAsync(
            tenantId, new PageRequest(page, pageSize, search), cancellationToken);
        if (result.IsFailure || result.Value is null)
            return BadRequest(new { error = result.Error });

        return Ok(PagedResponse("products", result.Value));
    }

    [Authorize(Policy = KnownPermissions.ProductsView)]
    [HttpGet("products/{productId:guid}")]
    public async Task<IActionResult> GetProduct(
        Guid productId, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        var result = await catalogManagementService.GetProductAsync(tenantId, productId, cancellationToken);
        if (result.IsFailure || result.Value is null)
            return NotFound(new { error = result.Error });

        return Ok(result.Value);
    }

    [Authorize(Policy = KnownPermissions.ProductsEdit)]
    [HttpPut("products/{productId:guid}")]
    public async Task<IActionResult> UpdateProduct(
        Guid productId, [FromBody] UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var command = new UpdateProductCommand
        {
            TenantId = request.TenantId,
            ProductId = productId,
            CategoryId = request.CategoryId,
            ProductTypeId = request.ProductTypeId,
            Name = request.Name,
            Slug = request.Slug,
            Description = request.Description,
            BaseSku = request.BaseSku,
            Barcode = request.Barcode,
            Brand = request.Brand,
            Status = request.Status,
            RequestedByUserId = currentUserService.UserId
        };

        var result = await catalogManagementService.UpdateProductAsync(command, cancellationToken);
        if (result.IsFailure || result.Value is null)
            return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }

    [Authorize(Policy = KnownPermissions.ProductsDisable)]
    [HttpDelete("products/{productId:guid}")]
    public async Task<IActionResult> DisableProduct(
        Guid productId, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        var result = await catalogManagementService.DisableProductAsync(tenantId, productId, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        return NoContent();
    }

    [Authorize(Policy = KnownPermissions.ProductsEdit)]
    [HttpPost("products/{productId:guid}/activate")]
    public async Task<IActionResult> ActivateProduct(
        Guid productId, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        var result = await catalogManagementService.ActivateProductAsync(tenantId, productId, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        return NoContent();
    }

    [Authorize(Policy = KnownPermissions.ProductsDisable)]
    [HttpDelete("products/{productId:guid}/delete")]
    public async Task<IActionResult> DeleteProduct(
        Guid productId, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        var result = await catalogManagementService.DeleteProductAsync(
            tenantId, productId, currentUserService.UserId, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        return NoContent();
    }

    [Authorize(Policy = KnownPermissions.ProductsView)]
    [HttpGet("products/{productId:guid}/variants")]
    public async Task<IActionResult> ListVariants(
        Guid productId, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        var result = await catalogManagementService.ListVariantsAsync(tenantId, productId, cancellationToken);
        if (result.IsFailure || result.Value is null)
            return BadRequest(new { error = result.Error });

        return Ok(new { variants = result.Value });
    }

    [Authorize(Policy = KnownPermissions.ProductsEdit)]
    [HttpPut("products/{productId:guid}/variants")]
    public async Task<IActionResult> UpdateVariants(
        Guid productId,
        [FromBody] UpdateProductVariantsCommand request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateProductVariantsCommand
        {
            TenantId = request.TenantId,
            ProductId = productId,
            Variants = request.Variants,
            RequestedByUserId = currentUserService.UserId
        };

        var result = await catalogManagementService.UpdateProductVariantsAsync(command, cancellationToken);
        if (result.IsFailure || result.Value is null)
            return BadRequest(new { error = result.Error });

        return Ok(new { variants = result.Value });
    }

    [Authorize(Policy = KnownPermissions.ProductsEdit)]
    [HttpPost("products/{productId:guid}/variants")]
    public async Task<IActionResult> AddVariants(
        Guid productId,
        [FromBody] AddProductVariantsCommand request,
        CancellationToken cancellationToken)
    {
        var command = new AddProductVariantsCommand
        {
            TenantId = request.TenantId,
            ProductId = productId,
            ProductName = request.ProductName,
            BaseSku = request.BaseSku,
            DefaultSalePrice = request.DefaultSalePrice,
            DefaultCostPrice = request.DefaultCostPrice,
            Status = request.Status,
            VariantAttributes = request.VariantAttributes,
            RequestedByUserId = currentUserService.UserId
        };

        var result = await productCatalogService.AddVariantsAsync(command, cancellationToken);
        if (result.IsFailure || result.Value is null)
            return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }

    [Authorize(Policy = KnownPermissions.ProductsEdit)]
    [HttpDelete("products/{productId:guid}/variants/{variantId:guid}")]
    public async Task<IActionResult> DeleteVariant(
        Guid productId,
        Guid variantId,
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken)
    {
        var result = await catalogManagementService.DeleteProductVariantAsync(
            tenantId, productId, variantId, currentUserService.UserId, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        return NoContent();
    }

    private static IDictionary<string, object> PagedResponse<T>(string itemsPropertyName, PagedResult<T> result) =>
        new Dictionary<string, object>
        {
            [itemsPropertyName] = result.Items,
            ["pagination"] = new
            {
                result.Page,
                result.PageSize,
                result.TotalCount,
                result.TotalPages
            }
        };
}
