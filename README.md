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

VS Code debug configuration:

- `Arca.Web (Admin)` runs on `http://localhost:5285`.
- `Arca.Api (External)` runs on `http://localhost:5286`.

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

## Stage 5 delivered

- Product image schema migration (`005_product_images_schema.sql`).
- Storage abstraction:
  - `IFileStorageService`
  - `FileUploadRequest`
  - `StoredFileResult`
- Local development storage in `src/Arca.Web/wwwroot/uploads`.
- S3-compatible storage implementation using configurable bucket, region, service URL, path-style mode and public base URL.
- Upload validation for:
  - max size: 5 MiB;
  - content types: JPEG, PNG, WebP and GIF;
  - extensions: `.jpg`, `.jpeg`, `.png`, `.webp`, `.gif`;
  - safe storage paths without traversal.
- Product image service and Dapper repository.
- Admin endpoints:
  - `GET /api/admin/catalog/products/{productId}/images?tenantId={tenantId}`
  - `POST /api/admin/catalog/products/{productId}/images`
  - `DELETE /api/admin/catalog/products/{productId}/images/{imageId}?tenantId={tenantId}`

In Development, uploaded product images are served from:

```text
/uploads/tenants/{tenantId}/products/{productId}/{imageId}.{ext}
```

## Stage 6 delivered

- Inventory schema migration (`006_inventory_schema.sql`) for:
  - `inventory_balance`;
  - `inventory_batch`;
  - `stock_movement`.
- Transactional inventory service and Dapper repository.
- Tenant/store/location/variant validation before every stock operation.
- Stock entry flow:
  - increases `InventoryBalance`;
  - creates `InventoryBatch` when batch or expiration data is provided;
  - records a `Purchase` stock movement.
- Stock exit flow:
  - checks available stock;
  - decreases `InventoryBalance`;
  - records `Sale`, `TransferOut`, `Loss` or `Consumption`.
- Stock adjustment flow:
  - sets the counted quantity;
  - optionally updates minimum stock;
  - records `Adjustment`.
- Admin endpoints:
  - `GET /api/admin/inventory/balance`
  - `GET /api/admin/inventory/movements`
  - `POST /api/admin/inventory/entries`
  - `POST /api/admin/inventory/exits`
  - `POST /api/admin/inventory/adjustments`

## Stage 7 delivered

- External API schema migration (`007_external_api_schema.sql`) for:
  - `api_client`;
  - `api_client_permission`;
  - `external_api_request_log`.
- API key generation with one-time plaintext return.
- API keys stored only as deterministic SHA-256 hashes.
- Admin API client endpoints:
  - `POST /api/admin/integrations/api-clients`
  - `GET /api/admin/integrations/api-clients?tenantId={tenantId}`
  - `DELETE /api/admin/integrations/api-clients/{apiClientId}?tenantId={tenantId}`
- External API authentication with:
  - `X-Api-Key: {key}`
  - `Authorization: Bearer {key}`
- Request logging for external API calls, including unauthorized requests.
- Basic fixed-window rate limiting prepared on `Arca.Api`.
- External catalog endpoints:
  - `GET /api/external/catalog/categories`
  - `GET /api/external/catalog/products`
  - `GET /api/external/catalog/products/{id}`
  - `GET /api/external/catalog/products/{id}/variants`
  - `GET /api/external/catalog/variants/{id}`
  - `GET /api/external/catalog/variants/{id}/images`
- External inventory endpoint:
  - `GET /api/external/inventory/availability?variantId={id}`

Run the external API locally:

```bash
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5286 dotnet run --project src/Arca.Api/Arca.Api.csproj
```

Example external request:

```bash
curl -H "X-Api-Key: {apiKey}" http://localhost:5286/api/external/catalog/products
```

## Domain layer alignment

The Domain layer now owns the core business concepts used by the database and application services:

- Tenancy: `Tenant`, `TenantSettings`, `Store`.
- Security: `User`, `Role`, `Permission`.
- Catalog: `Category`, `ProductType`, `ProductAttribute`, `ProductAttributeValue`, `Product`, `ProductVariant`, `ProductImage`.
- Inventory: `StockLocation`, `InventoryBalance`, `InventoryBatch`, `StockMovement`.
- Integrations/audit: `ApiClient`, `AuditLog`, `LoginAttempt`.
- Shared enums for role scope, product status, attribute type, storage provider and stock movement type.

Infrastructure keeps Dapper persistence and migrations; Application keeps orchestration/contracts; controllers remain thin entry points.

## Admin frontend shell delivered

- React + TypeScript + Vite app inside `src/Arca.Web/ClientApp`.
- Authenticated admin shell served by `Arca.Web`.
- Sidebar navigation for Dashboard, Tenants, Stores, Users, Roles, Catalog, Products, Variants, Inventory, Movements, Integrations and Settings.
- SuperAdmin tenant setup wizard consuming `POST /api/admin/tenants/setup`.
- Step-by-step tenant setup validation for required fields, email format, slug format, locale/currency format, unique store codes and temporary password length.
- Editable multi-store setup form with add/remove support.
- Initial Tenants screen for listing provisioned tenants and their stores.
- Initial Stores screen for listing, creating, editing and disabling stores by tenant.
- Initial Users screen for listing users, loading roles, creating users with scoped roles and disabling users.
- Initial Roles screen for listing roles, loading permissions, creating roles, updating permission sets and disabling non-system roles.
- Initial API Keys screen for listing, creating and disabling external API clients by tenant.
- Razor host view provides the current user metadata and antiforgery token to the React app.
- Production build output is generated into `src/Arca.Web/wwwroot/admin`.

Build the admin frontend:

```bash
cd src/Arca.Web/ClientApp
npm install
npm run build
```

## Stage 8 delivered

- Backend catalog CRUD endpoints for categories, product types, attributes, attribute values and full product management:
  - `GET/POST/PUT/DELETE /api/admin/catalog/categories`
  - `GET/POST/PUT/DELETE /api/admin/catalog/product-types`
  - `GET/POST/PUT/DELETE /api/admin/catalog/attributes` with nested value CRUD
  - `GET/GET:ID/PUT/DELETE /api/admin/catalog/products` with variant listing
  - `GET /api/admin/inventory/stock-locations` for stock location listing
- `CatalogManagementService` application layer with full validation.
- `DapperCatalogManagementRepository` covering all catalog CRUD operations.
- Frontend refactored from a single 1843-line `main.tsx` into a modular file structure:
  - Shared types (`types.ts`) and API client (`api.ts`)
  - Reusable components: `Field`, `Toggle`, `Sidebar`, `TenantSetupWizard`
  - Page components: `Dashboard`, `Tenants`, `Stores`, `Users`, `Roles`, `ApiKeys`
  - New admin screens: `Categories`, `ProductTypes`, `Attributes`, `Products`, `Inventory`
- React Router prepared (react-router-dom, react-hook-form, zod, @tanstack/react-query added as dependencies).
- Sidebar navigation with all catalog and inventory modules.
- Full product creation form with variant attribute selection and live preview.
- Product image management UI with upload, variant association, gallery listing and delete actions.
- Inventory management with balance check, stock entry/exit/adjustment forms and movement listing.
- Vite base path configured to `/admin/` for correct asset serving.

## Stage 9 delivered

- Unit test project: `tests/Arca.UnitTests`.
- Integration test project: `tests/Arca.IntegrationTests`.
- Unit coverage for:
  - product variant cartesian generation;
  - duplicate/existing SKU filtering;
  - catalog management validation and normalization.
- Integration coverage using Docker PostgreSQL with isolated temporary test databases:
  - SQL migrations applied end-to-end;
  - Dapper catalog repository persistence;
  - audit log persistence for catalog create/update operations.
- Catalog CRUD audit logging added for:
  - categories;
  - product types;
  - product attributes;
  - product attribute values;
  - product update/disable.
- Fixed category validation so creating a root category no longer fails by comparing `null` parent/category ids.

Run all tests:

```bash
dotnet test Arca.sln
```

## Stage 10 delivered

- Production hardening added to `Arca.Web` and `Arca.Api`:
  - forwarded header support for reverse proxies;
  - HSTS and HTTPS redirection outside Development;
  - secure response headers (`X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`);
  - request body and request header timeout limits through configuration;
  - configurable fixed-window rate limiting for admin, login and external API traffic.
- Production storage validation in `Arca.Web`:
  - non-Development startup requires `Storage:Provider = S3`;
  - required S3 bucket/region/access key/secret key settings are validated at startup.
- Admin list endpoints now support pagination and search:
  - `page`;
  - `pageSize` capped at 100;
  - `search`.
- Paginated/searchable endpoints:
  - `GET /api/admin/tenants`
  - `GET /api/admin/tenants/{tenantId}/stores`
  - `GET /api/admin/users`
  - `GET /api/admin/users/roles`
  - `GET /api/admin/roles`
  - `GET /api/admin/integrations/api-clients`
  - `GET /api/admin/catalog/categories`
  - `GET /api/admin/catalog/product-types`
  - `GET /api/admin/catalog/attributes`
  - `GET /api/admin/catalog/attributes/{attributeId}/values`
  - `GET /api/admin/catalog/products`
- Admin frontend search and pagination controls added to Tenants, Stores, Users, Roles, API Keys, Categories, Product Types, Attributes and Products.
- Integration test coverage added for pagination/search against PostgreSQL in catalog, tenants and API clients.

## Stage 11 delivered

- Container build manifests:
  - `Dockerfile.web` builds the React admin assets and publishes `Arca.Web`.
  - `Dockerfile.api` publishes the external integration API.
  - `.dockerignore` keeps build context small and excludes local uploads/secrets.
- Production Docker Compose manifest:
  - `deploy/docker-compose.prod.yml`;
  - separate admin Web and external API services;
  - production environment variables;
  - Docker secrets mounted at `/run/secrets`.
- Kubernetes manifests:
  - namespace;
  - shared ConfigMap;
  - example Secret;
  - Web/API deployments;
  - Web/API services;
  - ingress with separate admin/API hosts.
- Secret provider integration:
  - Web/API now load key-per-file secrets before production validation.
  - File names use double underscores, for example `ConnectionStrings__DefaultConnection`.
  - Kubernetes External Secrets example included at `deploy/k8s/external-secret.example.yaml`.
  - CSI/mounted secret providers are supported by setting `Secrets__KeyPerFile__Path`.
- Production startup validation now requires a database connection string in both Web/API, and S3 storage settings in Web.

## Current status against the initial prompt

Delivered foundation:

- Clean Architecture project split with Domain, Application, Infrastructure, Web and external API apps.
- PostgreSQL via Docker, Dapper access, SQL migrations and migration history.
- Cookie authentication for the admin panel, Argon2id password hashing, SuperAdmin seed and permission services.
- Tenant setup flow with tenant settings, stores, default stock location, TenantAdmin provisioning and initial catalog templates.
- Generic catalog foundation with configurable attributes and automatic variant generation.
- Product image upload/list/delete with local storage and S3-compatible storage.
- Inventory balance and movement flows for entry, exit and adjustment.
- External API with API Key/Bearer authentication, authorized public catalog, stock availability and request logs.
- Domain entities/enums aligned with the database concepts.
- Modular React admin shell with Sidebar navigation, validated tenant setup wizard and catalog/inventory admin screens.
- Backend catalog CRUD endpoints for categories, product types, attributes, attribute values, products and variants.
- Permission policies wired for admin endpoints, including tenant/store context resolution from route, query, form and JSON body.
- Unit and integration tests are wired into the solution.
- Catalog CRUD operations now write audit logs.
- Production hardening is prepared for Web/API startup and storage configuration.
- Admin list endpoints support pagination and search across tenant, user, role, API key and catalog screens.
- Production deploy manifests and secret-provider wiring are in place.

Still pending:

- Cloud-specific infrastructure as code, CI/CD pipeline and real registry/cluster values.
- Production operations: backups, monitoring dashboards, structured log shipping and alerting.
- Broader end-to-end test coverage for full admin workflows.
- Optional product refinements: invite email flow, report exports and dedicated audit log UI.
