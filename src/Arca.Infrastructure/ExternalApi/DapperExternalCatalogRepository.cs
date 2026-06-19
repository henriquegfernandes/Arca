using Arca.Application.Abstractions.ExternalApi;
using Arca.Application.ExternalApi;
using Arca.Infrastructure.Database;
using Dapper;

namespace Arca.Infrastructure.ExternalApi;

public sealed class DapperExternalCatalogRepository(IDbConnectionFactory connectionFactory) : IExternalCatalogRepository
{
    public async Task<IReadOnlyCollection<ExternalCategoryDto>> ListCategoriesAsync(
        ExternalApiClientContext client,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var categories = await connection.QueryAsync<ExternalCategoryDto>(new CommandDefinition(
            """
            SELECT
                id AS Id,
                parent_category_id AS ParentCategoryId,
                name AS Name,
                slug AS Slug,
                description AS Description,
                sort_order AS SortOrder
            FROM category
            WHERE tenant_id = @TenantId
              AND is_active = TRUE
            ORDER BY sort_order, name;
            """,
            new { client.TenantId },
            cancellationToken: cancellationToken));

        return categories.ToArray();
    }

    public async Task<IReadOnlyCollection<ExternalProductListItemDto>> ListProductsAsync(
        ExternalApiClientContext client,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var products = await connection.QueryAsync<ExternalProductListItemDto>(new CommandDefinition(
            """
            SELECT
                p.id AS Id,
                p.category_id AS CategoryId,
                p.product_type_id AS ProductTypeId,
                p.name AS Name,
                p.slug AS Slug,
                p.description AS Description,
                p.base_sku AS BaseSku,
                p.brand AS Brand,
                p.status AS Status,
                main_image.public_url AS MainImageUrl
            FROM product p
            LEFT JOIN LATERAL (
                SELECT public_url
                FROM product_image pi
                WHERE pi.product_id = p.id
                  AND pi.product_variant_id IS NULL
                ORDER BY pi.is_main DESC, pi.sort_order, pi.created_at
                LIMIT 1
            ) main_image ON TRUE
            WHERE p.tenant_id = @TenantId
              AND p.status = 'Active'
            ORDER BY p.name;
            """,
            new { client.TenantId },
            cancellationToken: cancellationToken));

        return products.ToArray();
    }

    public async Task<ExternalProductDto?> GetProductAsync(
        ExternalApiClientContext client,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var result = await connection.QueryMultipleAsync(new CommandDefinition(
            """
            SELECT
                id AS Id,
                category_id AS CategoryId,
                product_type_id AS ProductTypeId,
                name AS Name,
                slug AS Slug,
                description AS Description,
                base_sku AS BaseSku,
                barcode AS Barcode,
                brand AS Brand,
                status AS Status
            FROM product
            WHERE tenant_id = @TenantId
              AND id = @ProductId
              AND status = 'Active';

            SELECT
                id AS Id,
                product_id AS ProductId,
                product_variant_id AS ProductVariantId,
                file_name AS FileName,
                content_type AS ContentType,
                public_url AS PublicUrl,
                alt_text AS AltText,
                sort_order AS SortOrder,
                is_main AS IsMain
            FROM product_image
            WHERE product_id = @ProductId
            ORDER BY is_main DESC, sort_order, created_at;
            """,
            new { client.TenantId, ProductId = productId },
            cancellationToken: cancellationToken));

        var product = await result.ReadSingleOrDefaultAsync<ProductRecord>();
        if (product is null)
        {
            return null;
        }

        var images = (await result.ReadAsync<ExternalProductImageDto>()).ToArray();
        return new ExternalProductDto(
            product.Id,
            product.CategoryId,
            product.ProductTypeId,
            product.Name,
            product.Slug,
            product.Description,
            product.BaseSku,
            product.Barcode,
            product.Brand,
            product.Status,
            images);
    }

    public async Task<IReadOnlyCollection<ExternalProductVariantDto>> ListProductVariantsAsync(
        ExternalApiClientContext client,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        var variants = await LoadVariantsAsync(client, productId, null, cancellationToken);
        return variants;
    }

    public async Task<ExternalProductVariantDto?> GetVariantAsync(
        ExternalApiClientContext client,
        Guid variantId,
        CancellationToken cancellationToken = default)
    {
        var variants = await LoadVariantsAsync(client, null, variantId, cancellationToken);
        return variants.SingleOrDefault();
    }

    public async Task<IReadOnlyCollection<ExternalProductImageDto>> ListVariantImagesAsync(
        ExternalApiClientContext client,
        Guid variantId,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var images = await connection.QueryAsync<ExternalProductImageDto>(new CommandDefinition(
            """
            SELECT
                pi.id AS Id,
                pi.product_id AS ProductId,
                pi.product_variant_id AS ProductVariantId,
                pi.file_name AS FileName,
                pi.content_type AS ContentType,
                pi.public_url AS PublicUrl,
                pi.alt_text AS AltText,
                pi.sort_order AS SortOrder,
                pi.is_main AS IsMain
            FROM product_image pi
            INNER JOIN product_variant pv ON pv.id = COALESCE(pi.product_variant_id, @VariantId)
            INNER JOIN product p ON p.id = pv.product_id
            WHERE p.tenant_id = @TenantId
              AND pi.product_id = p.id
              AND pv.id = @VariantId
              AND p.status = 'Active'
              AND pv.status = 'Active'
              AND (pi.product_variant_id = @VariantId OR pi.product_variant_id IS NULL)
            ORDER BY pi.product_variant_id NULLS LAST, pi.is_main DESC, pi.sort_order, pi.created_at;
            """,
            new { client.TenantId, VariantId = variantId },
            cancellationToken: cancellationToken));

        return images.ToArray();
    }

    public async Task<ExternalInventoryAvailabilityDto?> GetAvailabilityAsync(
        ExternalApiClientContext client,
        Guid variantId,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var availability = await connection.QuerySingleOrDefaultAsync<ExternalInventoryAvailabilityDto>(new CommandDefinition(
            """
            SELECT
                pv.id AS ProductVariantId,
                CAST(@StoreId AS uuid) AS StoreId,
                COALESCE(SUM(ib.quantity - ib.reserved_quantity), 0)::int AS AvailableQuantity
            FROM product_variant pv
            INNER JOIN product p ON p.id = pv.product_id
            LEFT JOIN inventory_balance ib ON ib.product_variant_id = pv.id
            LEFT JOIN stock_location sl ON sl.id = ib.stock_location_id
            LEFT JOIN store s ON s.id = sl.store_id
            WHERE pv.id = @VariantId
              AND p.tenant_id = @TenantId
              AND p.status = 'Active'
              AND pv.status = 'Active'
              AND (@StoreId IS NULL OR s.id = @StoreId)
            GROUP BY pv.id;
            """,
            new { client.TenantId, StoreId = client.StoreId, VariantId = variantId },
            cancellationToken: cancellationToken));

        return availability;
    }

    private async Task<IReadOnlyCollection<ExternalProductVariantDto>> LoadVariantsAsync(
        ExternalApiClientContext client,
        Guid? productId,
        Guid? variantId,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        using var result = await connection.QueryMultipleAsync(new CommandDefinition(
            """
            SELECT
                pv.id AS Id,
                pv.product_id AS ProductId,
                pv.sku AS Sku,
                pv.barcode AS Barcode,
                pv.name AS Name,
                pv.default_sale_price AS DefaultSalePrice,
                pv.default_cost_price AS DefaultCostPrice,
                pv.status AS Status
            FROM product_variant pv
            INNER JOIN product p ON p.id = pv.product_id
            WHERE p.tenant_id = @TenantId
              AND p.status = 'Active'
              AND pv.status = 'Active'
              AND (@ProductId IS NULL OR pv.product_id = @ProductId)
              AND (@VariantId IS NULL OR pv.id = @VariantId)
            ORDER BY pv.name;

            SELECT
                pvav.product_variant_id AS ProductVariantId,
                pa.id AS ProductAttributeId,
                pa.name AS AttributeName,
                pav.id AS ProductAttributeValueId,
                pav.name AS ValueName,
                pav.code AS ValueCode
            FROM product_variant_attribute_value pvav
            INNER JOIN product_variant pv ON pv.id = pvav.product_variant_id
            INNER JOIN product p ON p.id = pv.product_id
            INNER JOIN product_attribute pa ON pa.id = pvav.product_attribute_id
            INNER JOIN product_attribute_value pav ON pav.id = pvav.product_attribute_value_id
            WHERE p.tenant_id = @TenantId
              AND (@ProductId IS NULL OR pv.product_id = @ProductId)
              AND (@VariantId IS NULL OR pv.id = @VariantId)
            ORDER BY pa.sort_order, pav.sort_order;
            """,
            new { client.TenantId, ProductId = productId, VariantId = variantId },
            cancellationToken: cancellationToken));

        var variants = (await result.ReadAsync<VariantRecord>()).ToArray();
        var attributes = (await result.ReadAsync<VariantAttributeRecord>())
            .GroupBy(attribute => attribute.ProductVariantId)
            .ToDictionary(group => group.Key, group => group
                .Select(attribute => new ExternalVariantAttributeDto(
                    attribute.ProductAttributeId,
                    attribute.AttributeName,
                    attribute.ProductAttributeValueId,
                    attribute.ValueName,
                    attribute.ValueCode))
                .ToArray());

        return variants
            .Select(variant => new ExternalProductVariantDto(
                variant.Id,
                variant.ProductId,
                variant.Sku,
                variant.Barcode,
                variant.Name,
                variant.DefaultSalePrice,
                variant.DefaultCostPrice,
                variant.Status,
                attributes.TryGetValue(variant.Id, out var variantAttributes) ? variantAttributes : []))
            .ToArray();
    }

    private sealed record ProductRecord(
        Guid Id,
        Guid? CategoryId,
        Guid? ProductTypeId,
        string Name,
        string Slug,
        string? Description,
        string BaseSku,
        string? Barcode,
        string? Brand,
        string Status);

    private sealed record VariantRecord(
        Guid Id,
        Guid ProductId,
        string Sku,
        string? Barcode,
        string Name,
        decimal DefaultSalePrice,
        decimal? DefaultCostPrice,
        string Status);

    private sealed record VariantAttributeRecord(
        Guid ProductVariantId,
        Guid ProductAttributeId,
        string AttributeName,
        Guid ProductAttributeValueId,
        string ValueName,
        string ValueCode);
}
