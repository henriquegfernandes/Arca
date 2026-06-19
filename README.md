# Arca

Enterprise multi-tenant e-commerce management platform with complete catalog, inventory and external API integration.

## Overview

Arca is a modern, production-ready multi-tenant SaaS platform for managing retail operations. It provides:

- **Multi-tenant architecture** with isolated data per tenant and configurable stores
- **Complete catalog management** with categories, product types, configurable attributes and automatic variant generation
- **Inventory management** with stock tracking, batch/expiration support and movement history
- **External API** for third-party integrations with API key authentication and rate limiting
- **Admin dashboard** with permission-based access, context-aware filtering and comprehensive audit logging
- **Production-ready deployment** with Docker, Kubernetes and cloud infrastructure support

## Technology Stack

- **Backend**: .NET 10 with Clean Architecture (Domain, Application, Infrastructure, Web, API layers)
- **Frontend**: React 19 + TypeScript + Vite with multi-language support
- **Database**: PostgreSQL with Dapper ORM and SQL migrations
- **Storage**: Local filesystem (dev) or S3-compatible object storage (production)
- **Authentication**: Cookie-based (admin), API Key (external integrations)
- **Security**: Argon2id password hashing, permission-based authorization, audit logging

## System Requirements

- .NET 10 SDK
- PostgreSQL 14+ (or Docker)
- Node.js 18+ (for admin frontend build)
- Docker & Docker Compose (optional, for local development PostgreSQL)

## Quick Start

### 1. Start PostgreSQL

Using Docker Compose:

```bash
docker compose up -d postgres
```

Or connect to an existing PostgreSQL instance by editing `appsettings.Development.json`.

### 2. Run the admin web app

```bash
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5285 \
  dotnet run --project src/Arca.Web/Arca.Web.csproj
```

The admin application automatically applies database migrations and seeds the SuperAdmin user on first run.

**Default admin credentials:**
- Email: `admin@arca.local`
- Password: `ChangeMe!12345`

Admin dashboard: `http://localhost:5285`

### 3. Run the external API (optional)

```bash
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5286 \
  dotnet run --project src/Arca.Api/Arca.Api.csproj
```

External API: `http://localhost:5286`

### 4. Build the admin frontend (optional)

```bash
cd src/Arca.Web/ClientApp
npm install
npm run build
```

## Development Configuration

**Connection string** (PostgreSQL on localhost:5433):
```
Host=localhost;Port=5433;Database=arca;Username=postgres;Password=postgres
```

**VS Code debug configurations:**
- `Arca.Web (Admin)` → `http://localhost:5285`
- `Arca.Api (External)` → `http://localhost:5286`

## Key Endpoints

### Admin API (Protected by cookie auth + permission policies)

- **Dashboard**: `GET /api/admin/dashboard/summary`
- **Tenants**: `GET/POST/PUT /api/admin/tenants`
- **Catalog**: `GET/POST/PUT/DELETE /api/admin/catalog/categories`, `products`, `attributes`, etc.
- **Inventory**: `GET/POST /api/admin/inventory/entries|exits|adjustments|movements`
- **Users/Roles/Permissions**: Full CRUD endpoints with role-based filtering
- **Audit Logs**: `GET /api/admin/audit-logs` with pagination, search and filtering
- **Reports**: `GET /api/admin/reports/products.csv|inventory.csv|movements.csv`

### External API (Protected by API Key)

```bash
curl -H "X-Api-Key: {apiKey}" http://localhost:5286/api/external/catalog/products
```

- **Catalog**: `GET /api/external/catalog/categories|products|variants`
- **Inventory**: `GET /api/external/inventory/availability`

## Testing

Run all tests (unit + integration):

```bash
dotnet test Arca.sln
```

Tests use Docker PostgreSQL with isolated temporary databases. Ensure Docker is running.

**Test coverage:**
- 9 unit tests for domain logic and validation
- 7 integration tests for database persistence and API contracts

## Production Deployment

### Docker Build

```bash
docker build -f Dockerfile.web -t arca-web .
docker build -f Dockerfile.api -t arca-api .
```

### Docker Compose

```bash
docker compose -f deploy/docker-compose.prod.yml up
```

### Kubernetes

```bash
kubectl apply -f deploy/k8s/
```

Includes:
- Deployments for Web and API with health probes
- Services for internal communication
- Ingress with separate admin/API hosts
- ConfigMap for environment configuration
- Secret provider integration examples

### Required Environment Variables (Production)

```bash
# Database
ConnectionStrings__DefaultConnection=postgres://user:pass@host:5432/arca

# Storage (S3-compatible)
Storage__Provider=S3
Storage__S3__Bucket=arca-bucket
Storage__S3__Region=us-east-1
Storage__S3__AccessKey=...
Storage__S3__SecretKey=...

# Email (optional)
Email__SmtpHost=smtp.example.com
Email__SmtpPort=587
Email__FromAddress=noreply@example.com

# Rate limiting (optional)
RateLimiting__AdminPerMinute=60
RateLimiting__LoginPerMinute=5
RateLimiting__ExternalApiPerSecond=10
```

## Architecture Overview

### Domain Layer
Core business concepts: `Tenant`, `Store`, `User`, `Category`, `Product`, `ProductVariant`, `InventoryBalance`, `ApiClient`, etc.

### Application Layer
Orchestration services: `TenantSetupService`, `CatalogManagementService`, `InventoryService`, `DashboardService`, etc.

### Infrastructure Layer
Persistence: Dapper repositories, SQL migrations, file storage abstraction (S3/local)

### Web Layer (Admin)
ASP.NET Core + React SPA with permission-based authorization, audit logging and context-aware data filtering

### API Layer (External)
Lightweight HTTP API for third-party integrations, API key authentication and request logging

## Key Features

- ✅ Multi-tenant data isolation with tenant/store context headers
- ✅ Configurable product catalog with automatic variant generation
- ✅ Real-time inventory tracking with batch and expiration support
- ✅ Complete audit logging for compliance
- ✅ Permission-based admin interface with role assignment
- ✅ External API with rate limiting and request logging
- ✅ Multi-language support (en-US, pt-BR)
- ✅ Responsive admin dashboard with real-time metrics
- ✅ Production-hardened deployment (HSTS, secure headers, rate limiting)

## Project Structure

```
src/
├── Arca.Domain/          # Business concepts and domain rules
├── Arca.Application/     # Services and application logic
├── Arca.Infrastructure/  # Persistence, migrations, file storage
├── Arca.Web/             # Admin app (ASP.NET Core + React SPA)
│   └── ClientApp/        # React frontend
└── Arca.Api/             # External integration API

tests/
├── Arca.UnitTests/       # Domain and service unit tests
└── Arca.IntegrationTests/# Database and API integration tests

deploy/
├── Dockerfile.web        # Admin app container
├── Dockerfile.api        # API app container
├── docker-compose.prod.yml
└── k8s/                  # Kubernetes manifests
```

## Contributing

This project follows Clean Architecture principles:
- Inward dependencies: Web/Api → Application → Domain; Infrastructure implements Domain interfaces
- Nullable reference types enabled globally
- Implicit usings enabled for .NET projects
- Zero-warning policy for TypeScript (ESLint `--max-warnings 0`)

## License

Proprietary - All rights reserved

## Support

For issues, questions or feedback, please visit the project repository.
