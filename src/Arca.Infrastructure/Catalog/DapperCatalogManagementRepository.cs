using Arca.Application.Abstractions.Catalog;
using Arca.Application.Catalog;
using Arca.Application.Common;
using Arca.Infrastructure.Database;
using Dapper;

namespace Arca.Infrastructure.Catalog;

public sealed class DapperCatalogManagementRepository(IDbConnectionFactory connectionFactory) : ICatalogManagementRepository
{
    public async Task<PagedResult<CategoryDto>> ListCategoriesAsync(
        Guid tenantId, PageRequest pageRequest, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var paging = NormalizePaging(pageRequest);
        var categories = await connection.QueryAsync<CategoryDto>(new CommandDefinition(
            """
            SELECT id, tenant_id AS TenantId, parent_category_id AS ParentCategoryId,
                   name, slug, description, sort_order AS SortOrder, is_active AS IsActive,
                   created_at AS CreatedAt
            FROM category
            WHERE tenant_id = @TenantId
              AND (@Search IS NULL OR name ILIKE @Search OR slug ILIKE @Search OR description ILIKE @Search)
            ORDER BY sort_order, name
            LIMIT @PageSize OFFSET @Offset;
            """,
            new { TenantId = tenantId, paging.Search, paging.PageSize, paging.Offset },
            cancellationToken: cancellationToken));

        var totalCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(*)::int
            FROM category
            WHERE tenant_id = @TenantId
              AND (@Search IS NULL OR name ILIKE @Search OR slug ILIKE @Search OR description ILIKE @Search);
            """,
            new { TenantId = tenantId, paging.Search },
            cancellationToken: cancellationToken));

        return new PagedResult<CategoryDto>(categories.ToArray(), totalCount, paging.Page, paging.PageSize);
    }

    public async Task<bool> CategorySlugExistsAsync(
        Guid tenantId, string slug, Guid? exceptId = null, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            exceptId is null
                ? "SELECT EXISTS (SELECT 1 FROM category WHERE tenant_id = @TenantId AND slug = @Slug);"
                : "SELECT EXISTS (SELECT 1 FROM category WHERE tenant_id = @TenantId AND slug = @Slug AND id != @ExceptId);",
            new { TenantId = tenantId, Slug = slug, ExceptId = exceptId },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> CategoryExistsAsync(
        Guid tenantId,
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT EXISTS (
                SELECT 1
                FROM category
                WHERE tenant_id = @TenantId
                  AND id = @CategoryId
                  AND is_active = TRUE
            );
            """,
            new { TenantId = tenantId, CategoryId = categoryId },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> CategoryIsDescendantAsync(
        Guid tenantId,
        Guid categoryId,
        Guid possibleDescendantId,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            WITH RECURSIVE descendants AS (
                SELECT id
                FROM category
                WHERE tenant_id = @TenantId
                  AND parent_category_id = @CategoryId

                UNION ALL

                SELECT c.id
                FROM category c
                INNER JOIN descendants d ON d.id = c.parent_category_id
                WHERE c.tenant_id = @TenantId
            )
            SELECT EXISTS (
                SELECT 1
                FROM descendants
                WHERE id = @PossibleDescendantId
            );
            """,
            new { TenantId = tenantId, CategoryId = categoryId, PossibleDescendantId = possibleDescendantId },
            cancellationToken: cancellationToken));
    }

    public async Task<CategoryDto> CreateCategoryAsync(
        CreateCategoryData data, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var now = DateTime.UtcNow;
        var id = Guid.NewGuid();

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO category (id, tenant_id, parent_category_id, name, slug, description, sort_order, is_active, created_at)
            VALUES (@Id, @TenantId, @ParentCategoryId, @Name, @Slug, @Description, @SortOrder, TRUE, @CreatedAt);
            """,
            new { Id = id, data.TenantId, data.ParentCategoryId, data.Name, data.Slug, data.Description, data.SortOrder, CreatedAt = now },
            transaction,
            cancellationToken: cancellationToken));

        await InsertAuditLogAsync(
            connection, transaction, data.RequestedByUserId, data.TenantId,
            "categories.create", "Category", id, null,
            FormatValue(("Name", data.Name), ("Slug", data.Slug)), now, cancellationToken);

        transaction.Commit();

        return new CategoryDto(id, data.TenantId, data.ParentCategoryId, data.Name, data.Slug,
            data.Description, data.SortOrder, true, now);
    }

    public async Task<CategoryDto?> UpdateCategoryAsync(
        UpdateCategoryData data, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var now = DateTime.UtcNow;

        var oldValue = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT CONCAT('Name=', name, '; Slug=', slug, '; SortOrder=', sort_order) FROM category WHERE tenant_id = @TenantId AND id = @CategoryId;",
            new { data.TenantId, data.CategoryId },
            transaction,
            cancellationToken: cancellationToken));

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE category
            SET parent_category_id = @ParentCategoryId, name = @Name, slug = @Slug,
                description = @Description, sort_order = @SortOrder, updated_at = @UpdatedAt
            WHERE tenant_id = @TenantId AND id = @CategoryId;
            """,
            new { data.TenantId, data.CategoryId, data.ParentCategoryId, data.Name, data.Slug,
                  data.Description, data.SortOrder, UpdatedAt = now },
            transaction,
            cancellationToken: cancellationToken));

        if (affected == 0) return null;

        await InsertAuditLogAsync(
            connection, transaction, data.RequestedByUserId, data.TenantId,
            "categories.update", "Category", data.CategoryId, oldValue,
            FormatValue(("Name", data.Name), ("Slug", data.Slug), ("SortOrder", data.SortOrder)), now, cancellationToken);

        transaction.Commit();

        return new CategoryDto(data.CategoryId, data.TenantId, data.ParentCategoryId, data.Name,
            data.Slug, data.Description, data.SortOrder, true, now);
    }

    public async Task<bool> DisableCategoryAsync(
        Guid tenantId, Guid categoryId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var now = DateTime.UtcNow;
        var oldValue = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT CONCAT('Name=', name, '; IsActive=', is_active) FROM category WHERE tenant_id = @TenantId AND id = @CategoryId;",
            new { TenantId = tenantId, CategoryId = categoryId },
            transaction,
            cancellationToken: cancellationToken));

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE category SET is_active = FALSE, updated_at = @UpdatedAt WHERE tenant_id = @TenantId AND id = @CategoryId;",
            new { TenantId = tenantId, CategoryId = categoryId, UpdatedAt = now },
            transaction,
            cancellationToken: cancellationToken));

        if (affected > 0)
        {
            await InsertAuditLogAsync(
                connection, transaction, null, tenantId, "categories.disable", "Category",
                categoryId, oldValue, "IsActive=False", now, cancellationToken);
        }

        transaction.Commit();
        return affected > 0;
    }

    public Task<bool> ActivateCategoryAsync(
        Guid tenantId, Guid categoryId, CancellationToken cancellationToken = default) =>
        SetBooleanActiveAsync("category", "id", "tenant_id = @TenantId AND id = @EntityId",
            tenantId, categoryId, true, "categories.activate", "Category", cancellationToken);

    public async Task<bool> DeleteCategoryAsync(
        Guid tenantId, Guid categoryId, Guid? requestedByUserId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var now = DateTime.UtcNow;
        var oldValue = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT CONCAT('Name=', name, '; Slug=', slug) FROM category WHERE tenant_id = @TenantId AND id = @CategoryId;",
            new { TenantId = tenantId, CategoryId = categoryId },
            transaction,
            cancellationToken: cancellationToken));

        if (oldValue is null)
        {
            transaction.Rollback();
            return false;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE product SET category_id = NULL, updated_at = @UpdatedAt WHERE tenant_id = @TenantId AND category_id = @CategoryId;",
            new { TenantId = tenantId, CategoryId = categoryId, UpdatedAt = now },
            transaction,
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE category SET parent_category_id = NULL, updated_at = @UpdatedAt WHERE tenant_id = @TenantId AND parent_category_id = @CategoryId;",
            new { TenantId = tenantId, CategoryId = categoryId, UpdatedAt = now },
            transaction,
            cancellationToken: cancellationToken));

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM category WHERE tenant_id = @TenantId AND id = @CategoryId;",
            new { TenantId = tenantId, CategoryId = categoryId },
            transaction,
            cancellationToken: cancellationToken));

        if (affected > 0)
        {
            await InsertAuditLogAsync(
                connection, transaction, requestedByUserId, tenantId, "categories.delete",
                "Category", categoryId, oldValue, "Deleted=True", now, cancellationToken);
        }

        transaction.Commit();
        return affected > 0;
    }

    public async Task<PagedResult<ProductTypeDto>> ListProductTypesAsync(
        Guid tenantId, PageRequest pageRequest, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var paging = NormalizePaging(pageRequest);
        var types = await connection.QueryAsync<ProductTypeDto>(new CommandDefinition(
            """
            SELECT id, tenant_id AS TenantId, name, description, is_active AS IsActive, created_at AS CreatedAt
            FROM product_type
            WHERE tenant_id = @TenantId
              AND (@Search IS NULL OR name ILIKE @Search OR description ILIKE @Search)
            ORDER BY name
            LIMIT @PageSize OFFSET @Offset;
            """,
            new { TenantId = tenantId, paging.Search, paging.PageSize, paging.Offset },
            cancellationToken: cancellationToken));

        var totalCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(*)::int
            FROM product_type
            WHERE tenant_id = @TenantId
              AND (@Search IS NULL OR name ILIKE @Search OR description ILIKE @Search);
            """,
            new { TenantId = tenantId, paging.Search },
            cancellationToken: cancellationToken));

        return new PagedResult<ProductTypeDto>(types.ToArray(), totalCount, paging.Page, paging.PageSize);
    }

    public async Task<bool> ProductTypeNameExistsAsync(
        Guid tenantId, string name, Guid? exceptId = null, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            exceptId is null
                ? "SELECT EXISTS (SELECT 1 FROM product_type WHERE tenant_id = @TenantId AND name = @Name);"
                : "SELECT EXISTS (SELECT 1 FROM product_type WHERE tenant_id = @TenantId AND name = @Name AND id != @ExceptId);",
            new { TenantId = tenantId, Name = name, ExceptId = exceptId },
            cancellationToken: cancellationToken));
    }

    public async Task<ProductTypeDto> CreateProductTypeAsync(
        CreateProductTypeData data, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var now = DateTime.UtcNow;
        var id = Guid.NewGuid();

        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO product_type (id, tenant_id, name, description, is_active, created_at) VALUES (@Id, @TenantId, @Name, @Description, TRUE, @CreatedAt);",
            new { Id = id, data.TenantId, data.Name, data.Description, CreatedAt = now },
            transaction,
            cancellationToken: cancellationToken));

        await InsertAuditLogAsync(
            connection, transaction, data.RequestedByUserId, data.TenantId,
            "product_types.create", "ProductType", id, null,
            FormatValue(("Name", data.Name)), now, cancellationToken);

        transaction.Commit();

        return new ProductTypeDto(id, data.TenantId, data.Name, data.Description, true, now);
    }

    public async Task<ProductTypeDto?> UpdateProductTypeAsync(
        UpdateProductTypeData data, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var now = DateTime.UtcNow;

        var oldValue = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT CONCAT('Name=', name) FROM product_type WHERE tenant_id = @TenantId AND id = @ProductTypeId;",
            new { data.TenantId, data.ProductTypeId },
            transaction,
            cancellationToken: cancellationToken));

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE product_type SET name = @Name, description = @Description, updated_at = @UpdatedAt WHERE tenant_id = @TenantId AND id = @ProductTypeId;",
            new { data.TenantId, data.ProductTypeId, data.Name, data.Description, UpdatedAt = now },
            transaction,
            cancellationToken: cancellationToken));

        if (affected == 0) return null;

        await InsertAuditLogAsync(
            connection, transaction, data.RequestedByUserId, data.TenantId,
            "product_types.update", "ProductType", data.ProductTypeId, oldValue,
            FormatValue(("Name", data.Name)), now, cancellationToken);

        transaction.Commit();

        return new ProductTypeDto(data.ProductTypeId, data.TenantId, data.Name, data.Description, true, now);
    }

    public async Task<bool> DisableProductTypeAsync(
        Guid tenantId, Guid productTypeId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var now = DateTime.UtcNow;
        var oldValue = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT CONCAT('Name=', name, '; IsActive=', is_active) FROM product_type WHERE tenant_id = @TenantId AND id = @ProductTypeId;",
            new { TenantId = tenantId, ProductTypeId = productTypeId },
            transaction,
            cancellationToken: cancellationToken));

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE product_type SET is_active = FALSE, updated_at = @UpdatedAt WHERE tenant_id = @TenantId AND id = @ProductTypeId;",
            new { TenantId = tenantId, ProductTypeId = productTypeId, UpdatedAt = now },
            transaction,
            cancellationToken: cancellationToken));

        if (affected > 0)
        {
            await InsertAuditLogAsync(
                connection, transaction, null, tenantId, "product_types.disable",
                "ProductType", productTypeId, oldValue, "IsActive=False", now, cancellationToken);
        }

        transaction.Commit();
        return affected > 0;
    }

    public Task<bool> ActivateProductTypeAsync(
        Guid tenantId, Guid productTypeId, CancellationToken cancellationToken = default) =>
        SetBooleanActiveAsync("product_type", "id", "tenant_id = @TenantId AND id = @EntityId",
            tenantId, productTypeId, true, "product_types.activate", "ProductType", cancellationToken);

    public async Task<bool> DeleteProductTypeAsync(
        Guid tenantId, Guid productTypeId, Guid? requestedByUserId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var now = DateTime.UtcNow;
        var oldValue = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT CONCAT('Name=', name) FROM product_type WHERE tenant_id = @TenantId AND id = @ProductTypeId;",
            new { TenantId = tenantId, ProductTypeId = productTypeId },
            transaction,
            cancellationToken: cancellationToken));

        if (oldValue is null)
        {
            transaction.Rollback();
            return false;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE product SET product_type_id = NULL, updated_at = @UpdatedAt WHERE tenant_id = @TenantId AND product_type_id = @ProductTypeId;",
            new { TenantId = tenantId, ProductTypeId = productTypeId, UpdatedAt = now },
            transaction,
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM product_type_attribute WHERE product_type_id = @ProductTypeId;",
            new { ProductTypeId = productTypeId },
            transaction,
            cancellationToken: cancellationToken));

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM product_type WHERE tenant_id = @TenantId AND id = @ProductTypeId;",
            new { TenantId = tenantId, ProductTypeId = productTypeId },
            transaction,
            cancellationToken: cancellationToken));

        if (affected > 0)
        {
            await InsertAuditLogAsync(
                connection, transaction, requestedByUserId, tenantId, "product_types.delete",
                "ProductType", productTypeId, oldValue, "Deleted=True", now, cancellationToken);
        }

        transaction.Commit();
        return affected > 0;
    }

    public async Task<IReadOnlyCollection<AttributeDto>> ListProductTypeAttributesAsync(
        Guid tenantId,
        Guid productTypeId,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var attributes = await connection.QueryAsync<AttributeDto>(new CommandDefinition(
            """
            SELECT
                pa.id AS Id,
                pa.tenant_id AS TenantId,
                pa.name AS Name,
                pa.code AS Code,
                pa.description AS Description,
                pa.attribute_type AS AttributeType,
                pta.is_variant_attribute AS IsVariantAttribute,
                pta.is_required AS IsRequired,
                pta.sort_order AS SortOrder,
                pa.is_active AS IsActive,
                pa.created_at AS CreatedAt
            FROM product_type_attribute pta
            INNER JOIN product_attribute pa ON pa.id = pta.product_attribute_id
            WHERE pta.product_type_id = @ProductTypeId
              AND pa.tenant_id = @TenantId
              AND pa.is_active = TRUE
            ORDER BY pta.sort_order, pa.sort_order, pa.name;
            """,
            new { TenantId = tenantId, ProductTypeId = productTypeId },
            cancellationToken: cancellationToken));

        return attributes.ToArray();
    }

    public async Task<PagedResult<AttributeDto>> ListAttributesAsync(
        Guid tenantId, PageRequest pageRequest, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var paging = NormalizePaging(pageRequest);
        var attributes = await connection.QueryAsync<AttributeDto>(new CommandDefinition(
            """
            SELECT id, tenant_id AS TenantId, name, code, description, attribute_type AS AttributeType,
                   is_variant_attribute AS IsVariantAttribute, is_required AS IsRequired,
                   sort_order AS SortOrder, is_active AS IsActive, created_at AS CreatedAt
            FROM product_attribute
            WHERE tenant_id = @TenantId
              AND (@Search IS NULL OR name ILIKE @Search OR code ILIKE @Search OR description ILIKE @Search)
            ORDER BY sort_order, name
            LIMIT @PageSize OFFSET @Offset;
            """,
            new { TenantId = tenantId, paging.Search, paging.PageSize, paging.Offset },
            cancellationToken: cancellationToken));

        var totalCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(*)::int
            FROM product_attribute
            WHERE tenant_id = @TenantId
              AND (@Search IS NULL OR name ILIKE @Search OR code ILIKE @Search OR description ILIKE @Search);
            """,
            new { TenantId = tenantId, paging.Search },
            cancellationToken: cancellationToken));

        return new PagedResult<AttributeDto>(attributes.ToArray(), totalCount, paging.Page, paging.PageSize);
    }

    public async Task<bool> AttributeCodeExistsAsync(
        Guid tenantId, string code, Guid? exceptId = null, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            exceptId is null
                ? "SELECT EXISTS (SELECT 1 FROM product_attribute WHERE tenant_id = @TenantId AND code = @Code);"
                : "SELECT EXISTS (SELECT 1 FROM product_attribute WHERE tenant_id = @TenantId AND code = @Code AND id != @ExceptId);",
            new { TenantId = tenantId, Code = code, ExceptId = exceptId },
            cancellationToken: cancellationToken));
    }

    public async Task<AttributeDto> CreateAttributeAsync(
        CreateAttributeData data, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var now = DateTime.UtcNow;
        var id = Guid.NewGuid();

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO product_attribute (id, tenant_id, name, code, description, attribute_type,
                                           is_variant_attribute, is_required, sort_order, is_active, created_at)
            VALUES (@Id, @TenantId, @Name, @Code, @Description, @AttributeType,
                    @IsVariantAttribute, @IsRequired, @SortOrder, TRUE, @CreatedAt);
            """,
            new { Id = id, data.TenantId, data.Name, data.Code, data.Description, data.AttributeType,
                  data.IsVariantAttribute, data.IsRequired, data.SortOrder, CreatedAt = now },
            transaction,
            cancellationToken: cancellationToken));

        await InsertAuditLogAsync(
            connection, transaction, data.RequestedByUserId, data.TenantId,
            "product_attributes.create", "ProductAttribute", id, null,
            FormatValue(("Name", data.Name), ("Code", data.Code), ("AttributeType", data.AttributeType)), now, cancellationToken);

        transaction.Commit();

        return new AttributeDto(id, data.TenantId, data.Name, data.Code, data.Description,
            data.AttributeType, data.IsVariantAttribute, data.IsRequired, data.SortOrder, true, now);
    }

    public async Task<AttributeDto?> UpdateAttributeAsync(
        UpdateAttributeData data, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var now = DateTime.UtcNow;

        var oldValue = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT CONCAT('Name=', name, '; Code=', code, '; AttributeType=', attribute_type) FROM product_attribute WHERE tenant_id = @TenantId AND id = @AttributeId;",
            new { data.TenantId, data.AttributeId },
            transaction,
            cancellationToken: cancellationToken));

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE product_attribute
            SET name = @Name, code = @Code, description = @Description, attribute_type = @AttributeType,
                is_variant_attribute = @IsVariantAttribute, is_required = @IsRequired,
                sort_order = @SortOrder, updated_at = @UpdatedAt
            WHERE tenant_id = @TenantId AND id = @AttributeId;
            """,
            new { data.TenantId, data.AttributeId, data.Name, data.Code, data.Description,
                  data.AttributeType, data.IsVariantAttribute, data.IsRequired, data.SortOrder, UpdatedAt = now },
            transaction,
            cancellationToken: cancellationToken));

        if (affected == 0) return null;

        await InsertAuditLogAsync(
            connection, transaction, data.RequestedByUserId, data.TenantId,
            "product_attributes.update", "ProductAttribute", data.AttributeId, oldValue,
            FormatValue(("Name", data.Name), ("Code", data.Code), ("AttributeType", data.AttributeType)), now, cancellationToken);

        transaction.Commit();

        return new AttributeDto(data.AttributeId, data.TenantId, data.Name, data.Code, data.Description,
            data.AttributeType, data.IsVariantAttribute, data.IsRequired, data.SortOrder, true, now);
    }

    public async Task<bool> DisableAttributeAsync(
        Guid tenantId, Guid attributeId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var now = DateTime.UtcNow;
        var oldValue = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT CONCAT('Name=', name, '; Code=', code, '; IsActive=', is_active) FROM product_attribute WHERE tenant_id = @TenantId AND id = @AttributeId;",
            new { TenantId = tenantId, AttributeId = attributeId },
            transaction,
            cancellationToken: cancellationToken));

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE product_attribute SET is_active = FALSE, updated_at = @UpdatedAt WHERE tenant_id = @TenantId AND id = @AttributeId;",
            new { TenantId = tenantId, AttributeId = attributeId, UpdatedAt = now },
            transaction,
            cancellationToken: cancellationToken));

        if (affected > 0)
        {
            await InsertAuditLogAsync(
                connection, transaction, null, tenantId, "product_attributes.disable",
                "ProductAttribute", attributeId, oldValue, "IsActive=False", now, cancellationToken);
        }

        transaction.Commit();
        return affected > 0;
    }

    public Task<bool> ActivateAttributeAsync(
        Guid tenantId, Guid attributeId, CancellationToken cancellationToken = default) =>
        SetBooleanActiveAsync("product_attribute", "id", "tenant_id = @TenantId AND id = @EntityId",
            tenantId, attributeId, true, "product_attributes.activate", "ProductAttribute", cancellationToken);

    public async Task<bool> DeleteAttributeAsync(
        Guid tenantId, Guid attributeId, Guid? requestedByUserId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var now = DateTime.UtcNow;
        var oldValue = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT CONCAT('Name=', name, '; Code=', code) FROM product_attribute WHERE tenant_id = @TenantId AND id = @AttributeId;",
            new { TenantId = tenantId, AttributeId = attributeId },
            transaction,
            cancellationToken: cancellationToken));

        if (oldValue is null)
        {
            transaction.Rollback();
            return false;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            """
            DELETE FROM product_variant_attribute_value pvav
            USING product_variant pv, product p
            WHERE pvav.product_variant_id = pv.id
              AND pv.product_id = p.id
              AND p.tenant_id = @TenantId
              AND pvav.product_attribute_id = @AttributeId;
            """,
            new { TenantId = tenantId, AttributeId = attributeId },
            transaction,
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            """
            DELETE FROM product_variant_option pvo
            USING product p
            WHERE pvo.product_id = p.id
              AND p.tenant_id = @TenantId
              AND pvo.product_attribute_id = @AttributeId;
            """,
            new { TenantId = tenantId, AttributeId = attributeId },
            transaction,
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            """
            DELETE FROM product_attribute_assignment paa
            USING product p
            WHERE paa.product_id = p.id
              AND p.tenant_id = @TenantId
              AND paa.product_attribute_id = @AttributeId;
            """,
            new { TenantId = tenantId, AttributeId = attributeId },
            transaction,
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM product_type_attribute WHERE product_attribute_id = @AttributeId;",
            new { AttributeId = attributeId },
            transaction,
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM product_attribute_value WHERE tenant_id = @TenantId AND product_attribute_id = @AttributeId;",
            new { TenantId = tenantId, AttributeId = attributeId },
            transaction,
            cancellationToken: cancellationToken));

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM product_attribute WHERE tenant_id = @TenantId AND id = @AttributeId;",
            new { TenantId = tenantId, AttributeId = attributeId },
            transaction,
            cancellationToken: cancellationToken));

        if (affected > 0)
        {
            await InsertAuditLogAsync(
                connection, transaction, requestedByUserId, tenantId, "product_attributes.delete",
                "ProductAttribute", attributeId, oldValue, "Deleted=True", now, cancellationToken);
        }

        transaction.Commit();
        return affected > 0;
    }

    public async Task<PagedResult<AttributeValueDto>> ListAttributeValuesAsync(
        Guid tenantId, Guid attributeId, PageRequest pageRequest, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var paging = NormalizePaging(pageRequest);
        var values = await connection.QueryAsync<AttributeValueDto>(new CommandDefinition(
            """
            SELECT id, tenant_id AS TenantId, product_attribute_id AS ProductAttributeId,
                   name, code, value, hex_code AS HexCode, sort_order AS SortOrder,
                   is_active AS IsActive, created_at AS CreatedAt
            FROM product_attribute_value
            WHERE tenant_id = @TenantId AND product_attribute_id = @AttributeId
              AND (@Search IS NULL OR name ILIKE @Search OR code ILIKE @Search OR value ILIKE @Search)
            ORDER BY sort_order, name
            LIMIT @PageSize OFFSET @Offset;
            """,
            new { TenantId = tenantId, AttributeId = attributeId, paging.Search, paging.PageSize, paging.Offset },
            cancellationToken: cancellationToken));

        var totalCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(*)::int
            FROM product_attribute_value
            WHERE tenant_id = @TenantId AND product_attribute_id = @AttributeId
              AND (@Search IS NULL OR name ILIKE @Search OR code ILIKE @Search OR value ILIKE @Search);
            """,
            new { TenantId = tenantId, AttributeId = attributeId, paging.Search },
            cancellationToken: cancellationToken));

        return new PagedResult<AttributeValueDto>(values.ToArray(), totalCount, paging.Page, paging.PageSize);
    }

    public async Task<bool> AttributeValueCodeExistsAsync(
        Guid tenantId, Guid attributeId, string code, Guid? exceptId = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            exceptId is null
                ? "SELECT EXISTS (SELECT 1 FROM product_attribute_value WHERE tenant_id = @TenantId AND product_attribute_id = @AttributeId AND code = @Code);"
                : "SELECT EXISTS (SELECT 1 FROM product_attribute_value WHERE tenant_id = @TenantId AND product_attribute_id = @AttributeId AND code = @Code AND id != @ExceptId);",
            new { TenantId = tenantId, AttributeId = attributeId, Code = code, ExceptId = exceptId },
            cancellationToken: cancellationToken));
    }

    public async Task<AttributeValueDto> CreateAttributeValueAsync(
        CreateAttributeValueData data, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var now = DateTime.UtcNow;
        var id = Guid.NewGuid();

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO product_attribute_value (id, tenant_id, product_attribute_id, name, code, value, hex_code, sort_order, is_active, created_at)
            VALUES (@Id, @TenantId, @ProductAttributeId, @Name, @Code, @Value, @HexCode, @SortOrder, TRUE, @CreatedAt);
            """,
            new { Id = id, data.TenantId, data.ProductAttributeId, data.Name, data.Code,
                  data.Value, data.HexCode, data.SortOrder, CreatedAt = now },
            transaction,
            cancellationToken: cancellationToken));

        await InsertAuditLogAsync(
            connection, transaction, data.RequestedByUserId, data.TenantId,
            "product_attribute_values.create", "ProductAttributeValue", id, null,
            FormatValue(("Name", data.Name), ("Code", data.Code)), now, cancellationToken);

        transaction.Commit();

        return new AttributeValueDto(id, data.TenantId, data.ProductAttributeId, data.Name,
            data.Code, data.Value, data.HexCode, data.SortOrder, true, now);
    }

    public async Task<AttributeValueDto?> UpdateAttributeValueAsync(
        UpdateAttributeValueData data, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var now = DateTime.UtcNow;

        var oldValue = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT CONCAT('Name=', name, '; Code=', code) FROM product_attribute_value WHERE tenant_id = @TenantId AND product_attribute_id = @ProductAttributeId AND id = @ValueId;",
            new { data.TenantId, data.ProductAttributeId, data.ValueId },
            transaction,
            cancellationToken: cancellationToken));

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE product_attribute_value
            SET name = @Name, code = @Code, value = @Value, hex_code = @HexCode,
                sort_order = @SortOrder, updated_at = @UpdatedAt
            WHERE tenant_id = @TenantId AND product_attribute_id = @ProductAttributeId AND id = @ValueId;
            """,
            new { data.TenantId, data.ProductAttributeId, data.ValueId, data.Name, data.Code,
                  data.Value, data.HexCode, data.SortOrder, UpdatedAt = now },
            transaction,
            cancellationToken: cancellationToken));

        if (affected == 0) return null;

        await InsertAuditLogAsync(
            connection, transaction, data.RequestedByUserId, data.TenantId,
            "product_attribute_values.update", "ProductAttributeValue", data.ValueId, oldValue,
            FormatValue(("Name", data.Name), ("Code", data.Code)), now, cancellationToken);

        transaction.Commit();

        return new AttributeValueDto(data.ValueId, data.TenantId, data.ProductAttributeId,
            data.Name, data.Code, data.Value, data.HexCode, data.SortOrder, true, now);
    }

    public async Task<bool> DisableAttributeValueAsync(
        Guid tenantId, Guid attributeId, Guid valueId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var now = DateTime.UtcNow;
        var oldValue = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT CONCAT('Name=', name, '; Code=', code, '; IsActive=', is_active) FROM product_attribute_value WHERE tenant_id = @TenantId AND product_attribute_id = @AttributeId AND id = @ValueId;",
            new { TenantId = tenantId, AttributeId = attributeId, ValueId = valueId },
            transaction,
            cancellationToken: cancellationToken));

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE product_attribute_value SET is_active = FALSE, updated_at = @UpdatedAt WHERE tenant_id = @TenantId AND product_attribute_id = @AttributeId AND id = @ValueId;",
            new { TenantId = tenantId, AttributeId = attributeId, ValueId = valueId, UpdatedAt = now },
            transaction,
            cancellationToken: cancellationToken));

        if (affected > 0)
        {
            await InsertAuditLogAsync(
                connection, transaction, null, tenantId, "product_attribute_values.disable",
                "ProductAttributeValue", valueId, oldValue, "IsActive=False", now, cancellationToken);
        }

        transaction.Commit();
        return affected > 0;
    }

    public Task<bool> ActivateAttributeValueAsync(
        Guid tenantId, Guid attributeId, Guid valueId, CancellationToken cancellationToken = default) =>
        SetBooleanActiveAsync("product_attribute_value", "id",
            "tenant_id = @TenantId AND product_attribute_id = @ParentId AND id = @EntityId",
            tenantId, valueId, true, "product_attribute_values.activate", "ProductAttributeValue",
            cancellationToken, attributeId);

    public async Task<bool> DeleteAttributeValueAsync(
        Guid tenantId,
        Guid attributeId,
        Guid valueId,
        Guid? requestedByUserId,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var now = DateTime.UtcNow;
        var oldValue = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT CONCAT('Name=', name, '; Code=', code) FROM product_attribute_value WHERE tenant_id = @TenantId AND product_attribute_id = @AttributeId AND id = @ValueId;",
            new { TenantId = tenantId, AttributeId = attributeId, ValueId = valueId },
            transaction,
            cancellationToken: cancellationToken));

        if (oldValue is null)
        {
            transaction.Rollback();
            return false;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            """
            DELETE FROM product_variant_attribute_value pvav
            USING product_variant pv, product p
            WHERE pvav.product_variant_id = pv.id
              AND pv.product_id = p.id
              AND p.tenant_id = @TenantId
              AND pvav.product_attribute_value_id = @ValueId;
            """,
            new { TenantId = tenantId, ValueId = valueId },
            transaction,
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            """
            DELETE FROM product_variant_option pvo
            USING product p
            WHERE pvo.product_id = p.id
              AND p.tenant_id = @TenantId
              AND pvo.product_attribute_value_id = @ValueId;
            """,
            new { TenantId = tenantId, ValueId = valueId },
            transaction,
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE product_attribute_assignment paa
            SET product_attribute_value_id = NULL
            FROM product p
            WHERE paa.product_id = p.id
              AND p.tenant_id = @TenantId
              AND paa.product_attribute_value_id = @ValueId;
            """,
            new { TenantId = tenantId, ValueId = valueId },
            transaction,
            cancellationToken: cancellationToken));

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM product_attribute_value WHERE tenant_id = @TenantId AND product_attribute_id = @AttributeId AND id = @ValueId;",
            new { TenantId = tenantId, AttributeId = attributeId, ValueId = valueId },
            transaction,
            cancellationToken: cancellationToken));

        if (affected > 0)
        {
            await InsertAuditLogAsync(
                connection, transaction, requestedByUserId, tenantId, "product_attribute_values.delete",
                "ProductAttributeValue", valueId, oldValue, "Deleted=True", now, cancellationToken);
        }

        transaction.Commit();
        return affected > 0;
    }

    public async Task<PagedResult<ProductSummaryDto>> ListProductsAsync(
        Guid tenantId, PageRequest pageRequest, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var paging = NormalizePaging(pageRequest);
        var products = await connection.QueryAsync<ProductSummaryDto>(new CommandDefinition(
            """
            SELECT p.id, p.tenant_id AS TenantId, p.category_id AS CategoryId, p.product_type_id AS ProductTypeId,
                   p.name, p.slug, p.description, p.base_sku AS BaseSku, p.barcode, p.brand, p.status,
                   main_image.public_url AS MainImageUrl,
                   (SELECT COUNT(*)::int FROM product_variant pv WHERE pv.product_id = p.id) AS VariantCount,
                   p.created_at AS CreatedAt
            FROM product p
            LEFT JOIN LATERAL (
                SELECT COALESCE(pi.public_url, pi.storage_path) AS public_url
                FROM product_image pi
                WHERE pi.product_id = p.id
                ORDER BY pi.is_main DESC, pi.sort_order, pi.created_at
                LIMIT 1
            ) main_image ON TRUE
            WHERE p.tenant_id = @TenantId
              AND (
                    @Search IS NULL
                 OR p.name ILIKE @Search
                 OR p.slug ILIKE @Search
                 OR p.base_sku ILIKE @Search
                 OR p.barcode ILIKE @Search
                 OR p.brand ILIKE @Search
                 OR p.status ILIKE @Search
              )
            ORDER BY p.created_at DESC
            LIMIT @PageSize OFFSET @Offset;
            """,
            new { TenantId = tenantId, paging.Search, paging.PageSize, paging.Offset },
            cancellationToken: cancellationToken));

        var totalCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(*)::int
            FROM product p
            WHERE p.tenant_id = @TenantId
              AND (
                    @Search IS NULL
                 OR p.name ILIKE @Search
                 OR p.slug ILIKE @Search
                 OR p.base_sku ILIKE @Search
                 OR p.barcode ILIKE @Search
                 OR p.brand ILIKE @Search
                 OR p.status ILIKE @Search
              );
            """,
            new { TenantId = tenantId, paging.Search },
            cancellationToken: cancellationToken));

        return new PagedResult<ProductSummaryDto>(products.ToArray(), totalCount, paging.Page, paging.PageSize);
    }

    public async Task<ProductDetailsDto?> GetProductAsync(
        Guid tenantId, Guid productId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<ProductDetailsDto>(new CommandDefinition(
            """
            SELECT p.id, p.tenant_id AS TenantId, p.category_id AS CategoryId,
                   c.name AS CategoryName, p.product_type_id AS ProductTypeId,
                   pt.name AS ProductTypeName, p.name, p.slug, p.description,
                   p.base_sku AS BaseSku, p.barcode, p.brand, p.status,
                   p.created_at AS CreatedAt, p.updated_at AS UpdatedAt
            FROM product p
            LEFT JOIN category c ON c.id = p.category_id AND c.tenant_id = @TenantId
            LEFT JOIN product_type pt ON pt.id = p.product_type_id AND pt.tenant_id = @TenantId
            WHERE p.tenant_id = @TenantId AND p.id = @ProductId;
            """,
            new { TenantId = tenantId, ProductId = productId },
            cancellationToken: cancellationToken));
    }

    public async Task<ProductSummaryDto?> UpdateProductAsync(
        UpdateProductData data, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var now = DateTime.UtcNow;

        var oldValue = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT CONCAT('Name=', name, '; BaseSku=', base_sku, '; Status=', status) FROM product WHERE tenant_id = @TenantId AND id = @ProductId;",
            new { data.TenantId, data.ProductId },
            transaction,
            cancellationToken: cancellationToken));

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE product
            SET category_id = @CategoryId, product_type_id = @ProductTypeId, name = @Name, slug = @Slug,
                description = @Description, base_sku = @BaseSku, barcode = @Barcode, brand = @Brand,
                status = @Status, updated_at = @UpdatedAt
            WHERE tenant_id = @TenantId AND id = @ProductId;
            """,
            new { data.TenantId, data.ProductId, data.CategoryId, data.ProductTypeId, data.Name,
                  data.Slug, data.Description, data.BaseSku, data.Barcode, data.Brand, data.Status, UpdatedAt = now },
            transaction,
            cancellationToken: cancellationToken));

        if (affected == 0) return null;

        await InsertAuditLogAsync(
            connection, transaction, data.RequestedByUserId, data.TenantId,
            "products.update", "Product", data.ProductId, oldValue,
            FormatValue(("Name", data.Name), ("BaseSku", data.BaseSku), ("Status", data.Status)), now, cancellationToken);

        transaction.Commit();

        return new ProductSummaryDto(data.ProductId, data.TenantId, data.CategoryId, data.ProductTypeId,
            data.Name, data.Slug, data.Description, data.BaseSku, data.Barcode, data.Brand,
            data.Status, null, 0, now);
    }

    public async Task<IReadOnlyCollection<ProductVariantDto>> UpdateProductVariantsAsync(
        UpdateProductVariantsData data,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var now = DateTime.UtcNow;

        try
        {
            var variantIds = data.Variants.Select(variant => variant.Id).ToArray();
            var requestedSkus = data.Variants.Select(variant => variant.Sku).ToArray();

            var ownedVariantCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                """
                SELECT COUNT(*)::int
                FROM product_variant pv
                INNER JOIN product p ON p.id = pv.product_id
                WHERE p.tenant_id = @TenantId
                  AND p.id = @ProductId
                  AND pv.id = ANY(@VariantIds);
                """,
                new { data.TenantId, data.ProductId, VariantIds = variantIds },
                transaction,
                cancellationToken: cancellationToken));

            if (ownedVariantCount != variantIds.Length)
            {
                throw new InvalidOperationException("One or more variants were not found for this product.");
            }

            var duplicatedSku = await connection.QueryFirstOrDefaultAsync<string>(new CommandDefinition(
                """
                SELECT pv.sku
                FROM product_variant pv
                WHERE pv.sku = ANY(@Skus)
                  AND pv.id <> ALL(@VariantIds)
                LIMIT 1;
                """,
                new { Skus = requestedSkus, VariantIds = variantIds },
                transaction,
                cancellationToken: cancellationToken));

            if (!string.IsNullOrWhiteSpace(duplicatedSku))
            {
                throw new InvalidOperationException($"Variant SKU is already in use: {duplicatedSku}.");
            }

            var oldValue = await connection.QueryAsync<string>(new CommandDefinition(
                """
                SELECT CONCAT('Sku=', sku, '; Price=', default_sale_price, '; Status=', status)
                FROM product_variant
                WHERE id = ANY(@VariantIds)
                ORDER BY sku;
                """,
                new { VariantIds = variantIds },
                transaction,
                cancellationToken: cancellationToken));

            foreach (var variant in data.Variants)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    UPDATE product_variant
                    SET sku = @Sku,
                        barcode = @Barcode,
                        name = @Name,
                        default_sale_price = @DefaultSalePrice,
                        default_cost_price = @DefaultCostPrice,
                        status = @Status,
                        updated_at = @UpdatedAt
                    WHERE id = @VariantId
                      AND product_id = @ProductId;
                    """,
                    new
                    {
                        VariantId = variant.Id,
                        data.ProductId,
                        variant.Sku,
                        variant.Barcode,
                        variant.Name,
                        variant.DefaultSalePrice,
                        variant.DefaultCostPrice,
                        variant.Status,
                        UpdatedAt = now
                    },
                    transaction,
                    cancellationToken: cancellationToken));
            }

            await InsertAuditLogAsync(
                connection, transaction, data.RequestedByUserId, data.TenantId,
                "product_variants.update", "Product", data.ProductId,
                string.Join(" | ", oldValue),
                $"UpdatedVariants={data.Variants.Count}", now, cancellationToken);

            transaction.Commit();
            return await ListVariantsAsync(data.TenantId, data.ProductId, cancellationToken);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<bool> DisableProductAsync(
        Guid tenantId, Guid productId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var now = DateTime.UtcNow;
        var oldValue = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT CONCAT('Name=', name, '; BaseSku=', base_sku, '; Status=', status) FROM product WHERE tenant_id = @TenantId AND id = @ProductId;",
            new { TenantId = tenantId, ProductId = productId },
            transaction,
            cancellationToken: cancellationToken));

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE product SET status = 'Inactive', updated_at = @UpdatedAt WHERE tenant_id = @TenantId AND id = @ProductId;",
            new { TenantId = tenantId, ProductId = productId, UpdatedAt = now },
            transaction,
            cancellationToken: cancellationToken));

        if (affected > 0)
        {
            await InsertAuditLogAsync(
                connection, transaction, null, tenantId, "products.disable",
                "Product", productId, oldValue, "Status=Inactive", now, cancellationToken);
        }

        transaction.Commit();
        return affected > 0;
    }

    public async Task<bool> ActivateProductAsync(
        Guid tenantId, Guid productId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var now = DateTime.UtcNow;
        var oldValue = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT CONCAT('Name=', name, '; BaseSku=', base_sku, '; Status=', status) FROM product WHERE tenant_id = @TenantId AND id = @ProductId;",
            new { TenantId = tenantId, ProductId = productId },
            transaction,
            cancellationToken: cancellationToken));

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE product SET status = 'Active', updated_at = @UpdatedAt WHERE tenant_id = @TenantId AND id = @ProductId;",
            new { TenantId = tenantId, ProductId = productId, UpdatedAt = now },
            transaction,
            cancellationToken: cancellationToken));

        if (affected > 0)
        {
            await InsertAuditLogAsync(
                connection, transaction, null, tenantId, "products.activate",
                "Product", productId, oldValue, "Status=Active", now, cancellationToken);
        }

        transaction.Commit();
        return affected > 0;
    }

    public async Task<bool> DeleteProductAsync(
        Guid tenantId, Guid productId, Guid? requestedByUserId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var now = DateTime.UtcNow;
        var oldValue = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT CONCAT('Name=', name, '; BaseSku=', base_sku, '; Status=', status) FROM product WHERE tenant_id = @TenantId AND id = @ProductId;",
            new { TenantId = tenantId, ProductId = productId },
            transaction,
            cancellationToken: cancellationToken));

        if (oldValue is null)
        {
            transaction.Rollback();
            return false;
        }

        const string variantFilter = "SELECT pv.id FROM product_variant pv INNER JOIN product p ON p.id = pv.product_id WHERE p.tenant_id = @TenantId AND p.id = @ProductId";

        await connection.ExecuteAsync(new CommandDefinition($"DELETE FROM stock_movement WHERE product_variant_id IN ({variantFilter});", new { TenantId = tenantId, ProductId = productId }, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition($"DELETE FROM inventory_batch WHERE product_variant_id IN ({variantFilter});", new { TenantId = tenantId, ProductId = productId }, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition($"DELETE FROM inventory_balance WHERE product_variant_id IN ({variantFilter});", new { TenantId = tenantId, ProductId = productId }, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition($"DELETE FROM store_product_variant WHERE product_variant_id IN ({variantFilter});", new { TenantId = tenantId, ProductId = productId }, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition("DELETE FROM product_image WHERE product_id = @ProductId;", new { ProductId = productId }, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition($"DELETE FROM product_variant_attribute_value WHERE product_variant_id IN ({variantFilter});", new { TenantId = tenantId, ProductId = productId }, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition("DELETE FROM product_variant WHERE product_id = @ProductId;", new { ProductId = productId }, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition("DELETE FROM product_variant_option WHERE product_id = @ProductId;", new { ProductId = productId }, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition("DELETE FROM product_attribute_assignment WHERE product_id = @ProductId;", new { ProductId = productId }, transaction, cancellationToken: cancellationToken));

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM product WHERE tenant_id = @TenantId AND id = @ProductId;",
            new { TenantId = tenantId, ProductId = productId },
            transaction,
            cancellationToken: cancellationToken));

        if (affected > 0)
        {
            await InsertAuditLogAsync(
                connection, transaction, requestedByUserId, tenantId, "products.delete",
                "Product", productId, oldValue, "Deleted=True", now, cancellationToken);
        }

        transaction.Commit();
        return affected > 0;
    }

    public async Task<IReadOnlyCollection<ProductVariantDto>> ListVariantsAsync(
        Guid tenantId, Guid productId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var variants = (await connection.QueryAsync<ProductVariantRecord>(new CommandDefinition(
            """
            SELECT pv.id, pv.product_id AS ProductId, pv.sku, pv.barcode, pv.name,
                   pv.default_sale_price AS DefaultSalePrice, pv.default_cost_price AS DefaultCostPrice,
                   pv.status, pv.created_at AS CreatedAt
            FROM product_variant pv
            INNER JOIN product p ON p.id = pv.product_id AND p.tenant_id = @TenantId
            WHERE p.tenant_id = @TenantId AND pv.product_id = @ProductId
            ORDER BY pv.sku;
            """,
            new { TenantId = tenantId, ProductId = productId },
            cancellationToken: cancellationToken))).ToArray();

        if (variants.Length == 0)
        {
            return [];
        }

        var variantIds = variants.Select(variant => variant.Id).ToArray();
        var attributes = (await connection.QueryAsync<ProductVariantAttributeRecord>(new CommandDefinition(
            """
            SELECT
                pvav.product_variant_id AS ProductVariantId,
                pa.id AS ProductAttributeId,
                pa.name AS AttributeName,
                pav.id AS ProductAttributeValueId,
                pav.name AS ValueName,
                pav.code AS Code
            FROM product_variant_attribute_value pvav
            INNER JOIN product_attribute pa ON pa.id = pvav.product_attribute_id
            INNER JOIN product_attribute_value pav ON pav.id = pvav.product_attribute_value_id
            WHERE pvav.product_variant_id = ANY(@VariantIds)
            ORDER BY pa.sort_order, pav.sort_order;
            """,
            new { VariantIds = variantIds },
            cancellationToken: cancellationToken)))
            .GroupBy(attribute => attribute.ProductVariantId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(attribute => new ProductVariantAttributeDto(
                        attribute.ProductAttributeId,
                        attribute.AttributeName,
                        attribute.ProductAttributeValueId,
                        attribute.ValueName,
                        attribute.Code))
                    .ToArray());

        return variants
            .Select(variant => new ProductVariantDto(
                variant.Id,
                variant.ProductId,
                variant.Sku,
                variant.Barcode,
                variant.Name,
                variant.DefaultSalePrice,
                variant.DefaultCostPrice,
                variant.Status,
                variant.CreatedAt,
                attributes.TryGetValue(variant.Id, out var values) ? values : []))
            .ToArray();
    }

    public async Task<bool> DeleteProductVariantAsync(
        Guid tenantId,
        Guid productId,
        Guid variantId,
        Guid? requestedByUserId,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var now = DateTime.UtcNow;

        var oldValue = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            """
            SELECT CONCAT('Sku=', pv.sku, '; Name=', pv.name)
            FROM product_variant pv
            INNER JOIN product p ON p.id = pv.product_id
            WHERE p.tenant_id = @TenantId
              AND p.id = @ProductId
              AND pv.id = @VariantId;
            """,
            new { TenantId = tenantId, ProductId = productId, VariantId = variantId },
            transaction,
            cancellationToken: cancellationToken));

        if (oldValue is null)
        {
            transaction.Rollback();
            return false;
        }

        await connection.ExecuteAsync(new CommandDefinition("DELETE FROM stock_movement WHERE product_variant_id = @VariantId;", new { VariantId = variantId }, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition("DELETE FROM inventory_batch WHERE product_variant_id = @VariantId;", new { VariantId = variantId }, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition("DELETE FROM inventory_balance WHERE product_variant_id = @VariantId;", new { VariantId = variantId }, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition("DELETE FROM store_product_variant WHERE product_variant_id = @VariantId;", new { VariantId = variantId }, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition("UPDATE product_image SET product_variant_id = NULL, updated_at = @UpdatedAt WHERE product_variant_id = @VariantId;", new { VariantId = variantId, UpdatedAt = now }, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition("DELETE FROM product_variant_attribute_value WHERE product_variant_id = @VariantId;", new { VariantId = variantId }, transaction, cancellationToken: cancellationToken));

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM product_variant WHERE id = @VariantId AND product_id = @ProductId;",
            new { VariantId = variantId, ProductId = productId },
            transaction,
            cancellationToken: cancellationToken));

        if (affected > 0)
        {
            await InsertAuditLogAsync(
                connection, transaction, requestedByUserId, tenantId,
                "product_variants.delete", "ProductVariant", variantId,
                oldValue, "Deleted=True", now, cancellationToken);
        }

        transaction.Commit();
        return affected > 0;
    }

    private sealed record ProductVariantRecord(
        Guid Id,
        Guid ProductId,
        string Sku,
        string? Barcode,
        string Name,
        decimal DefaultSalePrice,
        decimal? DefaultCostPrice,
        string Status,
        DateTime CreatedAt);

    private sealed record ProductVariantAttributeRecord(
        Guid ProductVariantId,
        Guid ProductAttributeId,
        string AttributeName,
        Guid ProductAttributeValueId,
        string ValueName,
        string Code);

    private async Task<bool> SetBooleanActiveAsync(
        string tableName,
        string idColumn,
        string whereClause,
        Guid tenantId,
        Guid entityId,
        bool isActive,
        string action,
        string entityName,
        CancellationToken cancellationToken,
        Guid? parentId = null)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var now = DateTime.UtcNow;

        var oldValue = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            $"SELECT CONCAT('IsActive=', is_active) FROM {tableName} WHERE {whereClause};",
            new { TenantId = tenantId, EntityId = entityId, ParentId = parentId },
            transaction,
            cancellationToken: cancellationToken));

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            $"UPDATE {tableName} SET is_active = @IsActive, updated_at = @UpdatedAt WHERE {whereClause};",
            new { TenantId = tenantId, EntityId = entityId, ParentId = parentId, IsActive = isActive, UpdatedAt = now },
            transaction,
            cancellationToken: cancellationToken));

        if (affected > 0)
        {
            await InsertAuditLogAsync(
                connection, transaction, null, tenantId, action, entityName,
                entityId, oldValue, $"IsActive={isActive}", now, cancellationToken);
        }

        transaction.Commit();
        return affected > 0;
    }

    private static Task InsertAuditLogAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction transaction,
        Guid? userId,
        Guid tenantId,
        string action,
        string entityName,
        Guid entityId,
        string? oldValue,
        string? newValue,
        DateTime createdAt,
        CancellationToken cancellationToken)
    {
        return connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO audit_log (
                id, user_id, tenant_id, store_id, action, entity_name, entity_id,
                old_value, new_value, ip_address, user_agent, created_at
            )
            VALUES (
                @Id, @UserId, @TenantId, NULL, @Action, @EntityName, @EntityId,
                @OldValue, @NewValue, NULL, NULL, @CreatedAt
            );
            """,
            new
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TenantId = tenantId,
                Action = action,
                EntityName = entityName,
                EntityId = entityId,
                OldValue = oldValue,
                NewValue = newValue,
                CreatedAt = createdAt
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static string FormatValue(params (string Key, object? Value)[] values) =>
        string.Join("; ", values.Select(value => $"{value.Key}={value.Value}"));

    private static (int Page, int PageSize, int Offset, string? Search) NormalizePaging(PageRequest request)
    {
        var search = request.NormalizedSearch is null ? null : $"%{EscapeLike(request.NormalizedSearch)}%";
        return (request.NormalizedPage, request.NormalizedPageSize, request.Offset, search);
    }

    private static string EscapeLike(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
