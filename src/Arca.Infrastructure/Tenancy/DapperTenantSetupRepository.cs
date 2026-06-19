using Arca.Application.Abstractions.Tenancy;
using Arca.Application.Security;
using Arca.Application.Tenancy;
using Arca.Infrastructure.Database;
using Dapper;

namespace Arca.Infrastructure.Tenancy;

public sealed class DapperTenantSetupRepository(IDbConnectionFactory connectionFactory) : ITenantSetupRepository
{
    private static readonly string[] TenantAdminPermissions =
    [
        KnownPermissions.TenantsView,
        KnownPermissions.TenantsEdit,
        KnownPermissions.StoresView,
        KnownPermissions.StoresCreate,
        KnownPermissions.StoresEdit,
        KnownPermissions.StoresDisable,
        KnownPermissions.UsersView,
        KnownPermissions.UsersCreate,
        KnownPermissions.UsersEdit,
        KnownPermissions.UsersChangePassword,
        KnownPermissions.UsersDisable,
        KnownPermissions.UsersAssignRoles,
        KnownPermissions.UsersAssignStores,
        KnownPermissions.CategoriesManage,
        KnownPermissions.ProductTypesManage,
        KnownPermissions.AttributesManage,
        KnownPermissions.ProductsView,
        KnownPermissions.ProductsCreate,
        KnownPermissions.ProductsEdit,
        KnownPermissions.ProductsDisable,
        KnownPermissions.InventoryView,
        KnownPermissions.InventoryEntry,
        KnownPermissions.InventoryExit,
        KnownPermissions.InventoryAdjust,
        KnownPermissions.InventoryTransfer,
        KnownPermissions.ReportsView,
        KnownPermissions.ReportsExport,
        KnownPermissions.ApiKeysManage
    ];

    public async Task<bool> TenantSlugExistsAsync(string slug, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM tenant WHERE slug = @Slug);",
            new { Slug = slug },
            cancellationToken: cancellationToken));
    }

    public async Task<TenantSetupResult> CreateAsync(
        TenantSetupData setupData,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var now = DateTime.UtcNow;
            var tenantId = Guid.NewGuid();
            var command = setupData.Command;

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO tenant (
                    id, name, legal_name, document, slug, contact_email, phone, main_segment,
                    is_active, setup_status, created_at, updated_at
                )
                VALUES (
                    @Id, @Name, @LegalName, @Document, @Slug, @ContactEmail, @Phone, @MainSegment,
                    TRUE, 'Completed', @CreatedAt, NULL
                );
                """,
                new
                {
                    Id = tenantId,
                    command.Company.Name,
                    command.Company.LegalName,
                    command.Company.Document,
                    command.Company.Slug,
                    ContactEmail = command.Company.Email,
                    command.Company.Phone,
                    command.Company.MainSegment,
                    CreatedAt = now
                },
                transaction,
                cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO tenant_settings (
                    id, tenant_id, currency, time_zone, default_language,
                    allow_multiple_stores, allow_batch_control, allow_expiration_control,
                    allow_store_specific_pricing, created_at, updated_at
                )
                VALUES (
                    @Id, @TenantId, @Currency, @TimeZone, @DefaultLanguage,
                    @AllowMultipleStores, @AllowBatchControl, @AllowExpirationControl,
                    @AllowStoreSpecificPricing, @CreatedAt, NULL
                );
                """,
                new
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    command.Settings.Currency,
                    command.Settings.TimeZone,
                    command.Settings.DefaultLanguage,
                    command.Settings.AllowMultipleStores,
                    command.Settings.AllowBatchControl,
                    command.Settings.AllowExpirationControl,
                    command.Settings.AllowStoreSpecificPricing,
                    CreatedAt = now
                },
                transaction,
                cancellationToken: cancellationToken));

            var storeIds = new List<Guid>();
            foreach (var store in command.Stores)
            {
                var storeId = Guid.NewGuid();
                storeIds.Add(storeId);

                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO store (
                        id, tenant_id, name, code, document, phone, email, address_line,
                        city, state, zip_code, type, is_active, created_at, updated_at
                    )
                    VALUES (
                        @Id, @TenantId, @Name, @Code, @Document, @Phone, @Email, @AddressLine,
                        @City, @State, @ZipCode, @Type, TRUE, @CreatedAt, NULL
                    );
                    """,
                    new
                    {
                        Id = storeId,
                        TenantId = tenantId,
                        Name = store.Name.Trim(),
                        Code = store.Code.Trim().ToUpperInvariant(),
                        Document = TrimToNull(store.Document),
                        Phone = TrimToNull(store.Phone),
                        Email = TrimToNull(store.Email),
                        AddressLine = TrimToNull(store.AddressLine),
                        City = TrimToNull(store.City),
                        State = TrimToNull(store.State),
                        ZipCode = TrimToNull(store.ZipCode),
                        Type = string.IsNullOrWhiteSpace(store.Type) ? "Store" : store.Type.Trim(),
                        CreatedAt = now
                    },
                    transaction,
                    cancellationToken: cancellationToken));

                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO stock_location (id, store_id, name, type, is_active, created_at, updated_at)
                    VALUES (@Id, @StoreId, 'Estoque Principal', 'Main', TRUE, @CreatedAt, NULL);
                    """,
                    new { Id = Guid.NewGuid(), StoreId = storeId, CreatedAt = now },
                    transaction,
                    cancellationToken: cancellationToken));
            }

            var administratorUserId = await UpsertTenantAdministratorAsync(connection, transaction, setupData, tenantId, now, cancellationToken);
            await SeedCatalogAsync(connection, transaction, tenantId, setupData.CatalogTemplate, now, cancellationToken);

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO audit_log (
                    id, user_id, tenant_id, store_id, action, entity_name, entity_id,
                    old_value, new_value, ip_address, user_agent, created_at
                )
                VALUES (
                    @Id, @UserId, @TenantId, NULL, 'tenants.setup', 'Tenant', @TenantId,
                    NULL, @NewValue, @IpAddress, @UserAgent, @CreatedAt
                );
                """,
                new
                {
                    Id = Guid.NewGuid(),
                    UserId = command.RequestedByUserId,
                    TenantId = tenantId,
                    NewValue = $"Template={setupData.CatalogTemplate.Name}; Stores={storeIds.Count}",
                    command.IpAddress,
                    command.UserAgent,
                    CreatedAt = now
                },
                transaction,
                cancellationToken: cancellationToken));

            transaction.Commit();

            return new TenantSetupResult(
                tenantId,
                storeIds,
                administratorUserId,
                setupData.CatalogTemplate.Name);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static async Task<Guid> UpsertTenantAdministratorAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction transaction,
        TenantSetupData setupData,
        Guid tenantId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var command = setupData.Command;
        var admin = command.Administrator;

        var userId = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            "SELECT id FROM app_user WHERE normalized_email = @NormalizedEmail;",
            new { NormalizedEmail = setupData.AdministratorNormalizedEmail },
            transaction,
            cancellationToken: cancellationToken));

        if (userId is null)
        {
            userId = Guid.NewGuid();
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO app_user (
                    id, full_name, email, normalized_email, phone, password_hash,
                    is_active, email_confirmed, last_login_at, created_at, updated_at
                )
                VALUES (
                    @Id, @FullName, @Email, @NormalizedEmail, @Phone, @PasswordHash,
                    TRUE, TRUE, NULL, @CreatedAt, NULL
                );
                """,
                new
                {
                    Id = userId.Value,
                    FullName = admin.FullName.Trim(),
                    Email = admin.Email.Trim(),
                    NormalizedEmail = setupData.AdministratorNormalizedEmail,
                    Phone = TrimToNull(admin.Phone),
                    PasswordHash = setupData.AdministratorPasswordHash,
                    CreatedAt = now
                },
                transaction,
                cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO user_tenant (id, user_id, tenant_id, is_active, created_at, updated_at)
            VALUES (@Id, @UserId, @TenantId, TRUE, @CreatedAt, NULL)
            ON CONFLICT (user_id, tenant_id)
            DO UPDATE SET is_active = TRUE, updated_at = EXCLUDED.created_at;
            """,
            new { Id = Guid.NewGuid(), UserId = userId.Value, TenantId = tenantId, CreatedAt = now },
            transaction,
            cancellationToken: cancellationToken));

        var roleId = Guid.NewGuid();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO role (
                id, tenant_id, name, normalized_name, description, scope,
                is_system_role, is_active, created_at, updated_at
            )
            VALUES (
                @Id, @TenantId, 'TenantAdmin', 'TENANTADMIN',
                'Tenant administrator', 'Tenant', FALSE, TRUE, @CreatedAt, NULL
            );
            """,
            new { Id = roleId, TenantId = tenantId, CreatedAt = now },
            transaction,
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO role_permission (role_id, permission_id)
            SELECT @RoleId, id
            FROM permission
            WHERE name = ANY(@PermissionNames)
            ON CONFLICT (role_id, permission_id) DO NOTHING;
            """,
            new { RoleId = roleId, PermissionNames = TenantAdminPermissions },
            transaction,
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO user_role (id, user_id, role_id, tenant_id, store_id, created_at)
            VALUES (@Id, @UserId, @RoleId, @TenantId, NULL, @CreatedAt);
            """,
            new { Id = Guid.NewGuid(), UserId = userId.Value, RoleId = roleId, TenantId = tenantId, CreatedAt = now },
            transaction,
            cancellationToken: cancellationToken));

        return userId.Value;
    }

    private static async Task SeedCatalogAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction transaction,
        Guid tenantId,
        CatalogTemplateDefinition template,
        DateTime now,
        CancellationToken cancellationToken)
    {
        foreach (var category in template.Categories)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO category (
                    id, tenant_id, parent_category_id, name, slug, description,
                    sort_order, is_active, created_at, updated_at
                )
                VALUES (
                    @Id, @TenantId, NULL, @Name, @Slug, @Description,
                    @SortOrder, TRUE, @CreatedAt, NULL
                );
                """,
                new
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    category.Name,
                    category.Slug,
                    category.Description,
                    category.SortOrder,
                    CreatedAt = now
                },
                transaction,
                cancellationToken: cancellationToken));
        }

        var attributeIdsByCode = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var attribute in template.Attributes)
        {
            var attributeId = Guid.NewGuid();
            attributeIdsByCode[attribute.Code] = attributeId;

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO product_attribute (
                    id, tenant_id, name, code, description, attribute_type,
                    is_variant_attribute, is_required, sort_order, is_active, created_at, updated_at
                )
                VALUES (
                    @Id, @TenantId, @Name, @Code, NULL, @AttributeType,
                    @IsVariantAttribute, @IsRequired, @SortOrder, TRUE, @CreatedAt, NULL
                );
                """,
                new
                {
                    Id = attributeId,
                    TenantId = tenantId,
                    attribute.Name,
                    attribute.Code,
                    attribute.AttributeType,
                    attribute.IsVariantAttribute,
                    attribute.IsRequired,
                    attribute.SortOrder,
                    CreatedAt = now
                },
                transaction,
                cancellationToken: cancellationToken));

            foreach (var value in attribute.Values)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO product_attribute_value (
                        id, tenant_id, product_attribute_id, name, code, value, hex_code,
                        sort_order, is_active, created_at, updated_at
                    )
                    VALUES (
                        @Id, @TenantId, @ProductAttributeId, @Name, @Code, @Value, @HexCode,
                        @SortOrder, TRUE, @CreatedAt, NULL
                    );
                    """,
                    new
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        ProductAttributeId = attributeId,
                        value.Name,
                        value.Code,
                        value.Value,
                        value.HexCode,
                        value.SortOrder,
                        CreatedAt = now
                    },
                    transaction,
                    cancellationToken: cancellationToken));
            }
        }

        foreach (var productType in template.ProductTypes)
        {
            var productTypeId = Guid.NewGuid();
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO product_type (
                    id, tenant_id, name, description, is_active, created_at, updated_at
                )
                VALUES (
                    @Id, @TenantId, @Name, @Description, TRUE, @CreatedAt, NULL
                );
                """,
                new
                {
                    Id = productTypeId,
                    TenantId = tenantId,
                    productType.Name,
                    productType.Description,
                    CreatedAt = now
                },
                transaction,
                cancellationToken: cancellationToken));

            foreach (var attribute in productType.Attributes)
            {
                if (!attributeIdsByCode.TryGetValue(attribute.AttributeCode, out var attributeId))
                {
                    continue;
                }

                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO product_type_attribute (
                        id, product_type_id, product_attribute_id,
                        is_required, is_variant_attribute, sort_order
                    )
                    VALUES (
                        @Id, @ProductTypeId, @ProductAttributeId,
                        @IsRequired, @IsVariantAttribute, @SortOrder
                    );
                    """,
                    new
                    {
                        Id = Guid.NewGuid(),
                        ProductTypeId = productTypeId,
                        ProductAttributeId = attributeId,
                        attribute.IsRequired,
                        attribute.IsVariantAttribute,
                        attribute.SortOrder
                    },
                    transaction,
                    cancellationToken: cancellationToken));
            }
        }
    }

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
