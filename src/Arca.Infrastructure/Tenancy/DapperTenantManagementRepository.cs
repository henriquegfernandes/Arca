using Arca.Application.Abstractions.Tenancy;
using Arca.Application.Common;
using Arca.Application.Tenancy;
using Arca.Infrastructure.Database;
using Dapper;

namespace Arca.Infrastructure.Tenancy;

public sealed class DapperTenantManagementRepository(IDbConnectionFactory connectionFactory) : ITenantManagementRepository
{
    public async Task<PagedResult<TenantSummaryDto>> ListTenantsAsync(
        PageRequest pageRequest,
        CancellationToken cancellationToken = default)
    {
        var paging = NormalizePaging(pageRequest);
        using var connection = connectionFactory.CreateConnection();
        using var result = await connection.QueryMultipleAsync(new CommandDefinition(
            """
            SELECT COUNT(*)
            FROM tenant t
            INNER JOIN tenant_settings ts ON ts.tenant_id = t.id
            WHERE @Search IS NULL
               OR t.name ILIKE @Search
               OR t.slug ILIKE @Search
               OR t.contact_email ILIKE @Search
               OR t.main_segment ILIKE @Search
               OR t.setup_status ILIKE @Search
               OR ts.currency ILIKE @Search;

            SELECT
                t.id AS Id,
                t.name AS Name,
                t.slug AS Slug,
                t.contact_email AS ContactEmail,
                t.main_segment AS MainSegment,
                t.is_active AS IsActive,
                t.setup_status AS SetupStatus,
                ts.currency AS Currency,
                ts.time_zone AS TimeZone,
                t.primary_store_id AS PrimaryStoreId,
                COUNT(s.id)::int AS StoreCount,
                t.created_at AS CreatedAt
            FROM tenant t
            INNER JOIN tenant_settings ts ON ts.tenant_id = t.id
            LEFT JOIN store s ON s.tenant_id = t.id
            WHERE @Search IS NULL
               OR t.name ILIKE @Search
               OR t.slug ILIKE @Search
               OR t.contact_email ILIKE @Search
               OR t.main_segment ILIKE @Search
               OR t.setup_status ILIKE @Search
               OR ts.currency ILIKE @Search
            GROUP BY
                t.id, t.name, t.slug, t.contact_email, t.main_segment,
                t.is_active, t.setup_status, ts.currency, ts.time_zone, t.primary_store_id, t.created_at
            ORDER BY t.created_at DESC
            LIMIT @PageSize OFFSET @Offset;
            """,
            new { paging.Search, paging.PageSize, paging.Offset },
            cancellationToken: cancellationToken));

        var totalCount = await result.ReadSingleAsync<int>();
        var tenants = (await result.ReadAsync<TenantSummaryDto>()).ToArray();
        return new PagedResult<TenantSummaryDto>(tenants, totalCount, paging.Page, paging.PageSize);
    }

    public async Task<TenantDetailsDto?> GetTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var result = await connection.QueryMultipleAsync(new CommandDefinition(
            """
            SELECT
                t.id AS Id,
                t.name AS Name,
                t.legal_name AS LegalName,
                t.document AS Document,
                t.slug AS Slug,
                t.contact_email AS ContactEmail,
                t.phone AS Phone,
                t.main_segment AS MainSegment,
                t.primary_store_id AS PrimaryStoreId,
                t.is_active AS IsActive,
                t.setup_status AS SetupStatus,
                ts.currency AS Currency,
                ts.time_zone AS TimeZone,
                ts.default_language AS DefaultLanguage,
                ts.allow_multiple_stores AS AllowMultipleStores,
                ts.allow_batch_control AS AllowBatchControl,
                ts.allow_expiration_control AS AllowExpirationControl,
                ts.allow_store_specific_pricing AS AllowStoreSpecificPricing,
                t.created_at AS CreatedAt,
                t.updated_at AS UpdatedAt
            FROM tenant t
            INNER JOIN tenant_settings ts ON ts.tenant_id = t.id
            WHERE t.id = @TenantId;

            SELECT
                id AS Id,
                tenant_id AS TenantId,
                name AS Name,
                code AS Code,
                type AS Type,
                document AS Document,
                phone AS Phone,
                email AS Email,
                address_line AS AddressLine,
                city AS City,
                state AS State,
                zip_code AS ZipCode,
                is_active AS IsActive,
                created_at AS CreatedAt
            FROM store
            WHERE tenant_id = @TenantId
            ORDER BY created_at, name;
            """,
            new { TenantId = tenantId },
            cancellationToken: cancellationToken));

        var row = await result.ReadSingleOrDefaultAsync<TenantDetailsRow>();
        if (row is null)
        {
            return null;
        }

        var stores = (await result.ReadAsync<StoreSummaryDto>()).ToArray();
        return new TenantDetailsDto(
            row.Id,
            row.Name,
            row.LegalName,
            row.Document,
            row.Slug,
            row.ContactEmail,
            row.Phone,
            row.MainSegment,
            row.PrimaryStoreId,
            row.IsActive,
            row.SetupStatus,
            new TenantSettingsDto(
                row.Currency,
                row.TimeZone,
                row.DefaultLanguage,
                row.AllowMultipleStores,
                row.AllowBatchControl,
                row.AllowExpirationControl,
                row.AllowStoreSpecificPricing),
            stores,
            row.CreatedAt,
            row.UpdatedAt);
    }

    public async Task<bool> TenantSlugExistsAsync(
        string slug,
        Guid? exceptTenantId = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT EXISTS (
                SELECT 1
                FROM tenant
                WHERE slug = @Slug
                  AND (@ExceptTenantId IS NULL OR id <> @ExceptTenantId)
            );
            """,
            new { Slug = slug, ExceptTenantId = exceptTenantId },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> StoreBelongsToTenantAsync(
        Guid tenantId,
        Guid storeId,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT EXISTS (
                SELECT 1
                FROM store
                WHERE id = @StoreId
                  AND tenant_id = @TenantId
                  AND is_active = TRUE
            );
            """,
            new { TenantId = tenantId, StoreId = storeId },
            cancellationToken: cancellationToken));
    }

    public async Task<TenantDetailsDto?> UpdateTenantAsync(
        UpdateTenantCommand command,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var now = DateTime.UtcNow;
            var oldValue = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
                "SELECT CONCAT('Name=', name, '; Slug=', slug, '; PrimaryStoreId=', COALESCE(primary_store_id::text, '')) FROM tenant WHERE id = @TenantId;",
                new { command.TenantId },
                transaction,
                cancellationToken: cancellationToken));

            var affected = await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE tenant
                SET name = @Name,
                    legal_name = @LegalName,
                    document = @Document,
                    slug = @Slug,
                    contact_email = @Email,
                    phone = @Phone,
                    main_segment = @MainSegment,
                    primary_store_id = @PrimaryStoreId,
                    updated_at = @UpdatedAt
                WHERE id = @TenantId;
                """,
                new
                {
                    command.TenantId,
                    command.Company.Name,
                    command.Company.LegalName,
                    command.Company.Document,
                    command.Company.Slug,
                    command.Company.Email,
                    command.Company.Phone,
                    command.Company.MainSegment,
                    command.PrimaryStoreId,
                    UpdatedAt = now
                },
                transaction,
                cancellationToken: cancellationToken));

            if (affected == 0)
            {
                transaction.Rollback();
                return null;
            }

            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE tenant_settings
                SET currency = @Currency,
                    time_zone = @TimeZone,
                    default_language = @DefaultLanguage,
                    allow_multiple_stores = @AllowMultipleStores,
                    allow_batch_control = @AllowBatchControl,
                    allow_expiration_control = @AllowExpirationControl,
                    allow_store_specific_pricing = @AllowStoreSpecificPricing,
                    updated_at = @UpdatedAt
                WHERE tenant_id = @TenantId;
                """,
                new
                {
                    command.TenantId,
                    command.Settings.Currency,
                    command.Settings.TimeZone,
                    command.Settings.DefaultLanguage,
                    command.Settings.AllowMultipleStores,
                    command.Settings.AllowBatchControl,
                    command.Settings.AllowExpirationControl,
                    command.Settings.AllowStoreSpecificPricing,
                    UpdatedAt = now
                },
                transaction,
                cancellationToken: cancellationToken));

            await InsertTenantAuditLogAsync(
                connection,
                transaction,
                command.RequestedByUserId,
                command.TenantId,
                "tenants.edit",
                oldValue,
                $"Name={command.Company.Name}; Slug={command.Company.Slug}; PrimaryStoreId={command.PrimaryStoreId}",
                command.IpAddress,
                command.UserAgent,
                now,
                cancellationToken);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        return await GetTenantAsync(command.TenantId, cancellationToken);
    }

    public async Task<PagedResult<StoreSummaryDto>> ListStoresAsync(
        Guid tenantId,
        PageRequest pageRequest,
        CancellationToken cancellationToken = default)
    {
        var paging = NormalizePaging(pageRequest);
        using var connection = connectionFactory.CreateConnection();
        using var result = await connection.QueryMultipleAsync(new CommandDefinition(
            """
            SELECT COUNT(*)
            FROM store
            WHERE tenant_id = @TenantId
              AND (
                    @Search IS NULL
                 OR name ILIKE @Search
                 OR code ILIKE @Search
                 OR type ILIKE @Search
                 OR email ILIKE @Search
                 OR phone ILIKE @Search
                 OR city ILIKE @Search
                 OR state ILIKE @Search
              );

            SELECT
                id AS Id,
                tenant_id AS TenantId,
                name AS Name,
                code AS Code,
                type AS Type,
                document AS Document,
                phone AS Phone,
                email AS Email,
                address_line AS AddressLine,
                city AS City,
                state AS State,
                zip_code AS ZipCode,
                is_active AS IsActive,
                created_at AS CreatedAt
            FROM store
            WHERE tenant_id = @TenantId
              AND (
                    @Search IS NULL
                 OR name ILIKE @Search
                 OR code ILIKE @Search
                 OR type ILIKE @Search
                 OR email ILIKE @Search
                 OR phone ILIKE @Search
                 OR city ILIKE @Search
                 OR state ILIKE @Search
              )
            ORDER BY created_at DESC, name
            LIMIT @PageSize OFFSET @Offset;
            """,
            new { TenantId = tenantId, paging.Search, paging.PageSize, paging.Offset },
            cancellationToken: cancellationToken));

        var totalCount = await result.ReadSingleAsync<int>();
        var stores = (await result.ReadAsync<StoreSummaryDto>()).ToArray();
        return new PagedResult<StoreSummaryDto>(stores, totalCount, paging.Page, paging.PageSize);
    }

    public async Task<bool> TenantExistsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM tenant WHERE id = @TenantId AND is_active = TRUE);",
            new { TenantId = tenantId },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> StoreCodeExistsAsync(
        Guid tenantId,
        string code,
        Guid? exceptStoreId = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT EXISTS (
                SELECT 1
                FROM store
                WHERE tenant_id = @TenantId
                  AND code = @Code
                  AND (@ExceptStoreId IS NULL OR id <> @ExceptStoreId)
            );
            """,
            new { TenantId = tenantId, Code = code, ExceptStoreId = exceptStoreId },
            cancellationToken: cancellationToken));
    }

    public async Task<StoreSummaryDto> CreateStoreAsync(
        CreateStoreCommand command,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var now = DateTime.UtcNow;
            var storeId = Guid.NewGuid();

            var store = await connection.QuerySingleAsync<StoreSummaryDto>(new CommandDefinition(
                """
                INSERT INTO store (
                    id, tenant_id, name, code, document, phone, email, address_line,
                    city, state, zip_code, type, is_active, created_at, updated_at
                )
                VALUES (
                    @Id, @TenantId, @Name, @Code, @Document, @Phone, @Email, @AddressLine,
                    @City, @State, @ZipCode, @Type, TRUE, @CreatedAt, NULL
                )
                RETURNING
                    id AS Id,
                    tenant_id AS TenantId,
                    name AS Name,
                    code AS Code,
                    type AS Type,
                    document AS Document,
                    phone AS Phone,
                    email AS Email,
                    address_line AS AddressLine,
                    city AS City,
                    state AS State,
                    zip_code AS ZipCode,
                    is_active AS IsActive,
                    created_at AS CreatedAt;
                """,
                new
                {
                    Id = storeId,
                    command.TenantId,
                    command.Name,
                    command.Code,
                    command.Document,
                    command.Phone,
                    command.Email,
                    command.AddressLine,
                    command.City,
                    command.State,
                    command.ZipCode,
                    command.Type,
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

            await InsertAuditLogAsync(
                connection,
                transaction,
                command.RequestedByUserId,
                command.TenantId,
                storeId,
                "stores.create",
                null,
                $"Name={command.Name}; Code={command.Code}",
                command.IpAddress,
                command.UserAgent,
                now,
                cancellationToken);

            transaction.Commit();
            return store;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<StoreSummaryDto?> UpdateStoreAsync(
        UpdateStoreCommand command,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var now = DateTime.UtcNow;
            var store = await connection.QuerySingleOrDefaultAsync<StoreSummaryDto>(new CommandDefinition(
                """
                UPDATE store
                SET name = @Name,
                    code = @Code,
                    document = @Document,
                    phone = @Phone,
                    email = @Email,
                    address_line = @AddressLine,
                    city = @City,
                    state = @State,
                    zip_code = @ZipCode,
                    type = @Type,
                    updated_at = @UpdatedAt
                WHERE id = @StoreId
                  AND tenant_id = @TenantId
                RETURNING
                    id AS Id,
                    tenant_id AS TenantId,
                    name AS Name,
                    code AS Code,
                    type AS Type,
                    document AS Document,
                    phone AS Phone,
                    email AS Email,
                    address_line AS AddressLine,
                    city AS City,
                    state AS State,
                    zip_code AS ZipCode,
                    is_active AS IsActive,
                    created_at AS CreatedAt;
                """,
                new
                {
                    command.TenantId,
                    command.StoreId,
                    command.Name,
                    command.Code,
                    command.Document,
                    command.Phone,
                    command.Email,
                    command.AddressLine,
                    command.City,
                    command.State,
                    command.ZipCode,
                    command.Type,
                    UpdatedAt = now
                },
                transaction,
                cancellationToken: cancellationToken));

            if (store is not null)
            {
                await InsertAuditLogAsync(
                    connection,
                    transaction,
                    command.RequestedByUserId,
                    command.TenantId,
                    command.StoreId,
                    "stores.edit",
                    null,
                    $"Name={command.Name}; Code={command.Code}",
                    command.IpAddress,
                    command.UserAgent,
                    now,
                    cancellationToken);
            }

            transaction.Commit();
            return store;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<bool> DisableStoreAsync(
        Guid tenantId,
        Guid storeId,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var now = DateTime.UtcNow;
            var affected = await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE store
                SET is_active = FALSE,
                    updated_at = @UpdatedAt
                WHERE id = @StoreId
                  AND tenant_id = @TenantId
                  AND is_active = TRUE;
                """,
                new { TenantId = tenantId, StoreId = storeId, UpdatedAt = now },
                transaction,
                cancellationToken: cancellationToken));

            if (affected > 0)
            {
                await InsertAuditLogAsync(
                    connection,
                    transaction,
                    null,
                    tenantId,
                    storeId,
                    "stores.disable",
                    null,
                    "IsActive=False",
                    null,
                    null,
                    now,
                    cancellationToken);
            }

            transaction.Commit();
            return affected > 0;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<bool> SetTenantActiveAsync(
        Guid tenantId,
        bool isActive,
        Guid? requestedByUserId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var now = DateTime.UtcNow;
            var affected = await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE tenant
                SET is_active = @IsActive,
                    updated_at = @UpdatedAt
                WHERE id = @TenantId
                  AND is_active <> @IsActive;
                """,
                new { TenantId = tenantId, IsActive = isActive, UpdatedAt = now },
                transaction,
                cancellationToken: cancellationToken));

            if (affected > 0)
            {
                await InsertTenantAuditLogAsync(
                    connection,
                    transaction,
                    requestedByUserId,
                    tenantId,
                    isActive ? "tenants.activate" : "tenants.disable",
                    $"IsActive={!isActive}",
                    $"IsActive={isActive}",
                    ipAddress,
                    userAgent,
                    now,
                    cancellationToken);
            }

            transaction.Commit();
            return affected > 0;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<bool> SetStoreActiveAsync(
        Guid tenantId,
        Guid storeId,
        bool isActive,
        Guid? requestedByUserId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var now = DateTime.UtcNow;
            var affected = await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE store
                SET is_active = @IsActive,
                    updated_at = @UpdatedAt
                WHERE id = @StoreId
                  AND tenant_id = @TenantId
                  AND is_active <> @IsActive;
                """,
                new { TenantId = tenantId, StoreId = storeId, IsActive = isActive, UpdatedAt = now },
                transaction,
                cancellationToken: cancellationToken));

            if (affected > 0)
            {
                await InsertAuditLogAsync(
                    connection,
                    transaction,
                    requestedByUserId,
                    tenantId,
                    storeId,
                    isActive ? "stores.activate" : "stores.disable",
                    $"IsActive={!isActive}",
                    $"IsActive={isActive}",
                    ipAddress,
                    userAgent,
                    now,
                    cancellationToken);
            }

            transaction.Commit();
            return affected > 0;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static Task InsertAuditLogAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction transaction,
        Guid? userId,
        Guid tenantId,
        Guid storeId,
        string action,
        string? oldValue,
        string? newValue,
        string? ipAddress,
        string? userAgent,
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
                @Id, @UserId, @TenantId, @StoreId, @Action, 'Store', @StoreId,
                @OldValue, @NewValue, @IpAddress, @UserAgent, @CreatedAt
            );
            """,
            new
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TenantId = tenantId,
                StoreId = storeId,
                Action = action,
                OldValue = oldValue,
                NewValue = newValue,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                CreatedAt = createdAt
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static Task InsertTenantAuditLogAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction transaction,
        Guid? userId,
        Guid tenantId,
        string action,
        string? oldValue,
        string? newValue,
        string? ipAddress,
        string? userAgent,
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
                @Id, @UserId, @TenantId, NULL, @Action, 'Tenant', @TenantId,
                @OldValue, @NewValue, @IpAddress, @UserAgent, @CreatedAt
            );
            """,
            new
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TenantId = tenantId,
                Action = action,
                OldValue = oldValue,
                NewValue = newValue,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                CreatedAt = createdAt
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private sealed class TenantDetailsRow
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? LegalName { get; init; }
        public string? Document { get; init; }
        public string Slug { get; init; } = string.Empty;
        public string? ContactEmail { get; init; }
        public string? Phone { get; init; }
        public string? MainSegment { get; init; }
        public Guid? PrimaryStoreId { get; init; }
        public bool IsActive { get; init; }
        public string SetupStatus { get; init; } = string.Empty;
        public string Currency { get; init; } = string.Empty;
        public string TimeZone { get; init; } = string.Empty;
        public string DefaultLanguage { get; init; } = string.Empty;
        public bool AllowMultipleStores { get; init; }
        public bool AllowBatchControl { get; init; }
        public bool AllowExpirationControl { get; init; }
        public bool AllowStoreSpecificPricing { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }
    }

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
