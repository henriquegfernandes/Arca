using Arca.Application.Abstractions.Catalog;
using Arca.Application.Catalog;
using Arca.Infrastructure.Database;
using Dapper;

namespace Arca.Infrastructure.Catalog;

public sealed class DapperProductCatalogRepository(IDbConnectionFactory connectionFactory) : IProductCatalogRepository
{
    public async Task<IReadOnlyCollection<VariantAttributeValueInfo>> GetVariantAttributeValuesAsync(
        Guid tenantId,
        IReadOnlyCollection<SelectedVariantAttribute> selectedAttributes,
        CancellationToken cancellationToken = default)
    {
        if (selectedAttributes.Count == 0)
        {
            return [];
        }

        var attributeIds = selectedAttributes.Select(attribute => attribute.ProductAttributeId).Distinct().ToArray();
        var valueIds = selectedAttributes
            .SelectMany(attribute => attribute.ProductAttributeValueIds)
            .Distinct()
            .ToArray();

        if (valueIds.Length == 0)
        {
            return [];
        }

        using var connection = connectionFactory.CreateConnection();
        var values = await connection.QueryAsync<VariantAttributeValueInfo>(new CommandDefinition(
            """
            SELECT
                pa.id AS ProductAttributeId,
                pa.name AS AttributeName,
                pa.sort_order AS AttributeSortOrder,
                pav.id AS ProductAttributeValueId,
                pav.name AS ValueName,
                pav.code AS ValueCode,
                pav.sort_order AS ValueSortOrder
            FROM product_attribute pa
            INNER JOIN product_attribute_value pav ON pav.product_attribute_id = pa.id
            WHERE pa.tenant_id = @TenantId
              AND pav.tenant_id = @TenantId
              AND pa.id = ANY(@AttributeIds)
              AND pav.id = ANY(@ValueIds)
              AND pa.is_active = TRUE
              AND pav.is_active = TRUE
            ORDER BY pa.sort_order, pav.sort_order;
            """,
            new { TenantId = tenantId, AttributeIds = attributeIds, ValueIds = valueIds },
            cancellationToken: cancellationToken));

        return values.ToArray();
    }

    public async Task<IReadOnlySet<string>> GetExistingSkusAsync(
        IReadOnlyCollection<string> skus,
        CancellationToken cancellationToken = default)
    {
        if (skus.Count == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        using var connection = connectionFactory.CreateConnection();
        var existing = await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT sku FROM product_variant WHERE sku = ANY(@Skus);",
            new { Skus = skus.ToArray() },
            cancellationToken: cancellationToken));

        return existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<bool> ProductSlugExistsAsync(
        Guid tenantId,
        string slug,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM product WHERE tenant_id = @TenantId AND slug = @Slug);",
            new { TenantId = tenantId, Slug = slug },
            cancellationToken: cancellationToken));
    }

    public async Task<CreateProductResult> CreateProductAsync(
        CreateProductData productData,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var now = DateTime.UtcNow;
            var command = productData.Command;
            var productId = Guid.NewGuid();

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO product (
                    id, tenant_id, category_id, product_type_id, name, slug, description,
                    base_sku, barcode, brand, status, created_at, updated_at
                )
                VALUES (
                    @Id, @TenantId, @CategoryId, @ProductTypeId, @Name, @Slug, @Description,
                    @BaseSku, @Barcode, @Brand, @Status, @CreatedAt, NULL
                );
                """,
                new
                {
                    Id = productId,
                    command.TenantId,
                    command.CategoryId,
                    command.ProductTypeId,
                    Name = command.ProductName,
                    command.Slug,
                    command.Description,
                    command.BaseSku,
                    command.Barcode,
                    command.Brand,
                    command.Status,
                    CreatedAt = now
                },
                transaction,
                cancellationToken: cancellationToken));

            foreach (var option in productData.VariantOptions)
            {
                foreach (var valueId in option.ProductAttributeValueIds.Distinct())
                {
                    await connection.ExecuteAsync(new CommandDefinition(
                        """
                        INSERT INTO product_variant_option (
                            id, product_id, product_attribute_id, product_attribute_value_id
                        )
                        VALUES (@Id, @ProductId, @ProductAttributeId, @ProductAttributeValueId)
                        ON CONFLICT (product_id, product_attribute_id, product_attribute_value_id) DO NOTHING;
                        """,
                        new
                        {
                            Id = Guid.NewGuid(),
                            ProductId = productId,
                            option.ProductAttributeId,
                            ProductAttributeValueId = valueId
                        },
                        transaction,
                        cancellationToken: cancellationToken));
                }
            }

            var createdVariants = new List<CreatedProductVariant>();
            foreach (var variant in productData.Variants)
            {
                var variantId = Guid.NewGuid();
                createdVariants.Add(new CreatedProductVariant(variantId, variant.Sku, variant.Name));

                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO product_variant (
                        id, product_id, sku, barcode, name, default_sale_price,
                        default_cost_price, status, created_at, updated_at
                    )
                    VALUES (
                        @Id, @ProductId, @Sku, NULL, @Name, @DefaultSalePrice,
                        @DefaultCostPrice, @Status, @CreatedAt, NULL
                    );
                    """,
                    new
                    {
                        Id = variantId,
                        ProductId = productId,
                        variant.Sku,
                        variant.Name,
                        variant.DefaultSalePrice,
                        variant.DefaultCostPrice,
                        variant.Status,
                        CreatedAt = now
                    },
                    transaction,
                    cancellationToken: cancellationToken));

                foreach (var attribute in variant.Attributes)
                {
                    await connection.ExecuteAsync(new CommandDefinition(
                        """
                        INSERT INTO product_variant_attribute_value (
                            id, product_variant_id, product_attribute_id, product_attribute_value_id
                        )
                        VALUES (@Id, @ProductVariantId, @ProductAttributeId, @ProductAttributeValueId);
                        """,
                        new
                        {
                            Id = Guid.NewGuid(),
                            ProductVariantId = variantId,
                            attribute.ProductAttributeId,
                            attribute.ProductAttributeValueId
                        },
                        transaction,
                        cancellationToken: cancellationToken));
                }
            }

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO audit_log (
                    id, user_id, tenant_id, store_id, action, entity_name, entity_id,
                    old_value, new_value, ip_address, user_agent, created_at
                )
                VALUES (
                    @Id, @UserId, @TenantId, NULL, 'products.create', 'Product', @ProductId,
                    NULL, @NewValue, NULL, NULL, @CreatedAt
                );
                """,
                new
                {
                    Id = Guid.NewGuid(),
                    UserId = command.RequestedByUserId,
                    command.TenantId,
                    ProductId = productId,
                    NewValue = $"BaseSku={command.BaseSku}; Variants={createdVariants.Count}",
                    CreatedAt = now
                },
                transaction,
                cancellationToken: cancellationToken));

            transaction.Commit();
            return new CreateProductResult(productId, createdVariants);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
