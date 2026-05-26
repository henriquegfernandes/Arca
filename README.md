# Arca

## Architectural notes (before coding)

This repository starts **Stage 1** of Arca with Clean Architecture boundaries and PostgreSQL + Dapper foundations.

### Suggested adjustments to the proposed design

1. Keep **Arca.Web** (cookie auth/admin) and **Arca.Api** (external integration) as separate apps, but centralize shared DI in `Arca.Infrastructure` extension methods to avoid duplicated startup code.
2. Use a migration naming convention `NNN_description.sql` and a migration history table (`schema_migrations`) from the beginning.
3. Normalize enum-like fields as constrained text (`CHECK`) initially, and migrate to lookup tables only if needed.
4. Add a global rule: every tenant-bound table must have an index on `tenant_id` (and `store_id` where applicable).
5. Keep authorization evaluation in Application services (policy handlers can call application abstractions), not directly in controllers.

## Stage 1 delivered

- Project folders for `Arca.Web`, `Arca.Api`, `Arca.Application`, `Arca.Domain`, `Arca.Infrastructure` and test projects.
- Initial .NET project files with references aligned to Clean Architecture.
- PostgreSQL connection factory abstraction and implementation.
- Basic health check wiring for Web and API apps.
- First SQL migration (`001_initial_schema.sql`) containing initial Tenancy tables and indexes.
