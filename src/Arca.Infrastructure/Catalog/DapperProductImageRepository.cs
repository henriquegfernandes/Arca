using Arca.Application.Abstractions.Catalog;
using Arca.Application.Catalog;
using Arca.Infrastructure.Database;
using Dapper;

namespace Arca.Infrastructure.Catalog;

public sealed class DapperProductImageRepository(IDbConnectionFactory connectionFactory) : IProductImageRepository
{
    public async Task<bool> ProductBelongsToTenantAsync(
        Guid tenantId,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM product WHERE id = @ProductId AND tenant_id = @TenantId);",
            new { TenantId = tenantId, ProductId = productId },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> VariantBelongsToProductAsync(
        Guid productId,
        Guid productVariantId,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM product_variant WHERE id = @ProductVariantId AND product_id = @ProductId);",
            new { ProductId = productId, ProductVariantId = productVariantId },
            cancellationToken: cancellationToken));
    }

    public async Task<ProductImageDto> AddAsync(
        AddProductImageData imageData,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var now = DateTime.UtcNow;

            if (imageData.IsMain)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    UPDATE product_image
                    SET is_main = FALSE, updated_at = @UpdatedAt
                    WHERE product_id = @ProductId
                      AND COALESCE(product_variant_id, '00000000-0000-0000-0000-000000000000'::uuid)
                          = COALESCE(@ProductVariantId, '00000000-0000-0000-0000-000000000000'::uuid);
                    """,
                    new { imageData.ProductId, imageData.ProductVariantId, UpdatedAt = now },
                    transaction,
                    cancellationToken: cancellationToken));
            }

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO product_image (
                    id, product_id, product_variant_id, file_name, original_file_name,
                    content_type, storage_provider, storage_path, public_url, alt_text,
                    sort_order, is_main, created_at, updated_at
                )
                VALUES (
                    @Id, @ProductId, @ProductVariantId, @FileName, @OriginalFileName,
                    @ContentType, @StorageProvider, @StoragePath, @PublicUrl, @AltText,
                    @SortOrder, @IsMain, @CreatedAt, NULL
                );
                """,
                new
                {
                    imageData.Id,
                    imageData.ProductId,
                    imageData.ProductVariantId,
                    imageData.FileName,
                    imageData.OriginalFileName,
                    imageData.ContentType,
                    imageData.StorageProvider,
                    imageData.StoragePath,
                    PublicUrl = imageData.PublicUrl,
                    imageData.AltText,
                    imageData.SortOrder,
                    imageData.IsMain,
                    CreatedAt = now
                },
                transaction,
                cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO audit_log (
                    id, user_id, tenant_id, store_id, action, entity_name, entity_id,
                    old_value, new_value, ip_address, user_agent, created_at
                )
                VALUES (
                    @Id, @UserId, @TenantId, NULL, 'product_images.create', 'ProductImage', @ImageId,
                    NULL, @NewValue, NULL, NULL, @CreatedAt
                );
                """,
                new
                {
                    Id = Guid.NewGuid(),
                    UserId = imageData.RequestedByUserId,
                    imageData.TenantId,
                    ImageId = imageData.Id,
                    NewValue = $"ProductId={imageData.ProductId}; StoragePath={imageData.StoragePath}",
                    CreatedAt = now
                },
                transaction,
                cancellationToken: cancellationToken));

            transaction.Commit();

            return new ProductImageDto(
                imageData.Id,
                imageData.ProductId,
                imageData.ProductVariantId,
                imageData.FileName,
                imageData.OriginalFileName,
                imageData.ContentType,
                imageData.StorageProvider,
                imageData.StoragePath,
                imageData.PublicUrl,
                imageData.AltText,
                imageData.SortOrder,
                imageData.IsMain,
                now);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<IReadOnlyCollection<ProductImageDto>> ListAsync(
        Guid tenantId,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var images = await connection.QueryAsync<ProductImageDto>(new CommandDefinition(
            """
            SELECT
                pi.id AS Id,
                pi.product_id AS ProductId,
                pi.product_variant_id AS ProductVariantId,
                pi.file_name AS FileName,
                pi.original_file_name AS OriginalFileName,
                pi.content_type AS ContentType,
                pi.storage_provider AS StorageProvider,
                pi.storage_path AS StoragePath,
                pi.public_url AS PublicUrl,
                pi.alt_text AS AltText,
                pi.sort_order AS SortOrder,
                pi.is_main AS IsMain,
                pi.created_at AS CreatedAt
            FROM product_image pi
            INNER JOIN product p ON p.id = pi.product_id
            WHERE p.tenant_id = @TenantId
              AND pi.product_id = @ProductId
            ORDER BY pi.sort_order, pi.created_at;
            """,
            new { TenantId = tenantId, ProductId = productId },
            cancellationToken: cancellationToken));

        return images.ToArray();
    }

    public async Task<ProductImageDto?> FindAsync(
        Guid tenantId,
        Guid productId,
        Guid imageId,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<ProductImageDto>(new CommandDefinition(
            """
            SELECT
                pi.id AS Id,
                pi.product_id AS ProductId,
                pi.product_variant_id AS ProductVariantId,
                pi.file_name AS FileName,
                pi.original_file_name AS OriginalFileName,
                pi.content_type AS ContentType,
                pi.storage_provider AS StorageProvider,
                pi.storage_path AS StoragePath,
                pi.public_url AS PublicUrl,
                pi.alt_text AS AltText,
                pi.sort_order AS SortOrder,
                pi.is_main AS IsMain,
                pi.created_at AS CreatedAt
            FROM product_image pi
            INNER JOIN product p ON p.id = pi.product_id
            WHERE p.tenant_id = @TenantId
              AND pi.product_id = @ProductId
              AND pi.id = @ImageId;
            """,
            new { TenantId = tenantId, ProductId = productId, ImageId = imageId },
            cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(
        Guid tenantId,
        Guid productId,
        Guid imageId,
        Guid? requestedByUserId,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                DELETE FROM product_image
                USING product
                WHERE product.id = product_image.product_id
                  AND product.tenant_id = @TenantId
                  AND product_image.product_id = @ProductId
                  AND product_image.id = @ImageId;
                """,
                new { TenantId = tenantId, ProductId = productId, ImageId = imageId },
                transaction,
                cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO audit_log (
                    id, user_id, tenant_id, store_id, action, entity_name, entity_id,
                    old_value, new_value, ip_address, user_agent, created_at
                )
                VALUES (
                    @Id, @UserId, @TenantId, NULL, 'product_images.delete', 'ProductImage', @ImageId,
                    NULL, NULL, NULL, NULL, @CreatedAt
                );
                """,
                new
                {
                    Id = Guid.NewGuid(),
                    UserId = requestedByUserId,
                    TenantId = tenantId,
                    ImageId = imageId,
                    CreatedAt = DateTime.UtcNow
                },
                transaction,
                cancellationToken: cancellationToken));

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
