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

## Local development

The solution targets `net10.0` to match the local SDK/runtime.

Run the admin Web app:

```bash
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5285 dotnet run --project src/Arca.Web/Arca.Web.csproj
```

In Development, the app uses:

- PostgreSQL connection: `Host=localhost;Port=5432;Database=arca;Username=postgres;Password=postgres`
- Login: `admin@arca.local`
- Password: `ChangeMe!12345`
