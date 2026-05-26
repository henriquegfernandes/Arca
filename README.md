# Arca

## Architectural notes (before coding)

This repository starts Arca with Clean Architecture boundaries and PostgreSQL + Dapper foundations.

### Suggested adjustments to the proposed design

1. Keep **Arca.Web** (cookie auth/admin) and **Arca.Api** (external integration) as separate apps, but centralize shared DI in `Arca.Infrastructure` extension methods to avoid duplicated startup code.
2. Use a migration naming convention `NNN_description.sql` and a migration history table (`schema_migrations`) from the beginning.
3. Normalize enum-like fields as constrained text (`CHECK`) initially, and migrate to lookup tables only if needed.
4. Add a global rule: every tenant-bound table must have an index on `tenant_id` (and `store_id` where applicable).
5. Keep authorization evaluation in Application services (policy handlers can call application abstractions), not directly in controllers.

## Stage 1 delivered

- Project folders for `Arca.Web`, `Arca.Api`, `Arca.Application`, `Arca.Domain` and `Arca.Infrastructure`.
- Initial .NET project files with references aligned to Clean Architecture.
- PostgreSQL connection factory abstraction and implementation.
- Basic health check wiring for Web and API apps.
- First SQL migration (`001_initial_schema.sql`) containing initial Tenancy tables and indexes.

## Stage 2 delivered

- Cookie authentication for the admin Web app.
- Argon2id password hashing with versioned hash format.
- Auth schema migration (`002_auth_schema.sql`) for users, roles, permissions, login attempts and scoped user assignments.
- Database migration runner with `schema_migration` tracking.
- Development-only database creation when `Database:CreateDatabaseIfMissing` is enabled.
- SuperAdmin seed with all initial permissions.
- `ICurrentUserService`, `IPermissionService` and `ITenantAccessService` abstractions with initial implementations.
- Minimal `/login`, `/logout`, `/` and `/health` routes for end-to-end testing.

## Stage 3 delivered

- Docker Compose PostgreSQL service for local development.
- Development connection string now targets the Docker PostgreSQL instance on `localhost:5433`.
- Tenant setup schema migration (`003_tenant_setup_schema.sql`) for:
  - tenant contact/setup fields;
  - stock locations;
  - initial catalog configuration tables;
  - audit logs.
- `TenantSetupService` application orchestration.
- `CatalogTemplateSeeder` with initial templates for `Fashion`, `Shoes`, `Electronics`, `ReligiousGoods`, `FoodBakery`, `SnackBarRestaurant`, `Market` and `Custom`.
- Tenant administrator provisioning with Argon2id password hash.
- Dapper transactional setup repository.
- SuperAdmin-protected endpoint: `POST /api/admin/tenants/setup`.

## Local development

The solution targets `net10.0` to match the local SDK/runtime.

Start PostgreSQL:

```bash
docker compose up -d postgres
```

Run the admin Web app:

```bash
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5285 dotnet run --project src/Arca.Web/Arca.Web.csproj
```

In Development, the app uses:

- PostgreSQL connection: `Host=localhost;Port=5433;Database=arca;Username=postgres;Password=postgres`
- Login: `admin@arca.local`
- Password: `ChangeMe!12345`

Create a tenant setup after signing in as SuperAdmin by posting JSON to:

```text
POST http://localhost:5285/api/admin/tenants/setup
```

The endpoint is protected by cookie authentication, SuperAdmin authorization and antiforgery validation.

## Stage 4 delivered

- Product catalog schema migration (`004_product_catalog_schema.sql`) for:
  - products;
  - product attribute assignments;
  - selected variant options;
  - product variants;
  - variant attribute values;
  - store-specific variant availability/pricing base table.
- `ProductVariantGenerator` with cartesian variant generation.
- SKU generation from `BaseSku + attribute value codes`.
- Duplicate SKU filtering during preview/create.
- Product catalog service and Dapper repository.
- Admin endpoints:
  - `POST /api/admin/catalog/variants/preview`
  - `POST /api/admin/catalog/products`

Example variant generation:

```text
BaseSku: CAM-SB
Color: PRE, BRA
Size: P, M

Generated:
CAM-SB-PRE-P
CAM-SB-PRE-M
CAM-SB-BRA-P
CAM-SB-BRA-M
```
