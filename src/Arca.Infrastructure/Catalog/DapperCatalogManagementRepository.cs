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

    public async Task<PagedResult<ProductSummaryDto>> ListProductsAsync(
        Guid tenantId, PageRequest pageRequest, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var paging = NormalizePaging(pageRequest);
        var products = await connection.QueryAsync<ProductSummaryDto>(new CommandDefinition(
            """
            SELECT p.id, p.tenant_id AS TenantId, p.category_id AS CategoryId, p.product_type_id AS ProductTypeId,
                   p.name, p.slug, p.description, p.base_sku AS BaseSku, p.barcode, p.brand, p.status,
                   (SELECT COUNT(*) FROM product_variant pv WHERE pv.product_id = p.id) AS VariantCount,
                   p.created_at AS CreatedAt
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
            data.Status, 0, now);
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

    public async Task<IReadOnlyCollection<ProductVariantDto>> ListVariantsAsync(
        Guid tenantId, Guid productId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var variants = await connection.QueryAsync<ProductVariantDto>(new CommandDefinition(
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
            cancellationToken: cancellationToken));

        return variants.ToArray();
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
