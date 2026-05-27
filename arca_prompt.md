Você será responsável por iniciar o desenvolvimento de um sistema chamado Arca.

Arca é um sistema SaaS multi-tenant e multi-store para gerenciamento de estoque, catálogo de produtos, lojas/franquias, usuários, permissões e integrações externas com sites/e-commerces.

O sistema deve ser genérico o suficiente para atender diferentes tipos de comércio, como:
- lojas de roupas;
- calçados;
- artigos religiosos;
- eletrônicos;
- alimentos;
- padarias;
- lanchonetes;
- mercados;
- lojas de presentes;
- pequenos comércios em geral.

Apesar de o primeiro uso real ser para uma loja de moda streetwear, a arquitetura e o banco de dados não devem ser nichados para moda. O sistema deve usar um modelo flexível de atributos configuráveis para produtos e variações.

Stack principal:
- Backend: ASP.NET Core
- Frontend: React + TypeScript
- Banco de dados: PostgreSQL
- Acesso a dados: Dapper
- Autenticação: Cookie Authentication para o painel web
- Hash de senha: Argon2id
- Autorização: Roles + Permissions + Policies
- API externa: autenticação por API Key ou Bearer Token para integração com sites/e-commerces
- Storage de imagens:
  - Produção: S3-compatible bucket
  - Desenvolvimento local: pasta local dentro do projeto
- Arquitetura: Clean Architecture
- Foco em performance, segurança, organização e manutenibilidade

============================================================
1. OBJETIVO DO SISTEMA
============================================================

O Arca deve permitir:

- Criar e gerenciar tenants.
- Criar e gerenciar lojas/franquias dentro de um tenant.
- Cadastrar usuários.
- Definir quais tenants e lojas cada usuário pode acessar.
- Trabalhar com escopos de permissão:
  - System
  - Tenant
  - Store
- Ter um usuário SuperAdmin da plataforma.
- Ter usuários TenantAdmin para administrar apenas um tenant.
- Ter usuários de loja, como StoreManager, StockOperator, Seller e Viewer.
- Configurar categorias hierárquicas.
- Configurar tipos de produto.
- Configurar atributos de produto.
- Configurar valores de atributos.
- Criar produtos genéricos.
- Gerar variações automaticamente com base nos atributos selecionados.
- Gerenciar estoque por loja/local.
- Registrar movimentações de estoque.
- Controlar imagens dos produtos.
- Expor API externa para consulta de produtos, categorias, variações, preços, imagens e estoque disponível.

============================================================
2. ARQUITETURA DO PROJETO
============================================================

Estruture o projeto em Clean Architecture.

Sugestão de estrutura:

src/
  Arca.Web/
    Controllers/
    Views/
    ClientApp/
    wwwroot/
    Program.cs
    appsettings.json
    appsettings.Development.json

  Arca.Api/
    Controllers/
    Middlewares/
    Filters/
    Program.cs

  Arca.Application/
    Abstractions/
    DTOs/
    UseCases/
    Services/
    Validators/
    Security/
    Storage/
    Catalog/
    Tenancy/
    Inventory/

  Arca.Domain/
    Entities/
    Enums/
    ValueObjects/
    Constants/
    Exceptions/

  Arca.Infrastructure/
    Database/
    Repositories/
    Dapper/
    Migrations/
    Auth/
    Storage/
    ExternalApi/
    Seed/

tests/
  Arca.UnitTests/
  Arca.IntegrationTests/

Responsabilidades:

Arca.Domain:
- Entidades.
- Enums.
- Regras de domínio.
- Value Objects.
- Nenhuma dependência de infraestrutura.

Arca.Application:
- Casos de uso.
- DTOs.
- Interfaces de repositórios.
- Interfaces de serviços.
- Validações.
- Regras de aplicação.
- Orquestração.

Arca.Infrastructure:
- Dapper.
- PostgreSQL.
- Repositórios concretos.
- Migrações.
- Implementação de storage local/S3.
- Implementação de Argon2id.
- Implementação de serviços externos.
- Seeds iniciais.

Arca.Web:
- Aplicação MVC com React para o painel administrativo.
- Cookie authentication.
- Views base para servir o React.
- Controllers internos do painel.

Arca.Api:
- API externa para integração com sites/e-commerces.
- Deve ser separada conceitualmente da área administrativa.
- Deve autenticar usando API Key ou Bearer Token próprio.
- Não deve depender de cookie auth.

============================================================
3. FRONTEND
============================================================

Use React com TypeScript dentro do projeto ASP.NET.

O painel administrativo deve ser moderno, fluido e responsivo.

Stack sugerida:
- React
- TypeScript
- Vite
- React Router
- React Hook Form
- Zod
- TanStack Query
- Framer Motion
- Shadcn/UI ou componentes próprios bem organizados

O frontend deve consumir endpoints internos do backend.

Principais áreas do painel:
- Login
- Dashboard
- Tenants, visível apenas para SuperAdmin
- Lojas
- Usuários
- Roles e permissões
- Categorias
- Tipos de produto
- Atributos de produto
- Produtos
- Variações
- Estoque
- Movimentações
- Configurações
- Integrações/API Keys

Criar um wizard para setup de novo tenant com as etapas:

1. Empresa
2. Configurações
3. Lojas
4. Administrador
5. Catálogo inicial
6. Revisão

Esse wizard deve ser usado pelo SuperAdmin para provisionar um novo tenant.

============================================================
4. BANCO DE DADOS
============================================================

Use PostgreSQL.

Use Dapper para acesso a dados.

Evite EF Core.

Crie migrations SQL versionadas no projeto Infrastructure.

Tabelas principais:

------------------------------------------------------------
4.1 Tenancy
------------------------------------------------------------

Tenant
- Id UUID PK
- Name VARCHAR NOT NULL
- LegalName VARCHAR NULL
- Document VARCHAR NULL
- Slug VARCHAR NOT NULL UNIQUE
- IsActive BOOLEAN NOT NULL
- SetupStatus VARCHAR NOT NULL
- CreatedAt TIMESTAMP NOT NULL
- UpdatedAt TIMESTAMP NULL

TenantSettings
- Id UUID PK
- TenantId UUID FK Tenant(Id)
- Currency VARCHAR NOT NULL
- TimeZone VARCHAR NOT NULL
- DefaultLanguage VARCHAR NOT NULL
- AllowMultipleStores BOOLEAN NOT NULL
- AllowBatchControl BOOLEAN NOT NULL
- AllowExpirationControl BOOLEAN NOT NULL
- AllowStoreSpecificPricing BOOLEAN NOT NULL
- CreatedAt TIMESTAMP NOT NULL
- UpdatedAt TIMESTAMP NULL

Store
- Id UUID PK
- TenantId UUID FK Tenant(Id)
- Name VARCHAR NOT NULL
- Code VARCHAR NOT NULL
- Document VARCHAR NULL
- Phone VARCHAR NULL
- Email VARCHAR NULL
- AddressLine VARCHAR NULL
- City VARCHAR NULL
- State VARCHAR NULL
- ZipCode VARCHAR NULL
- Type VARCHAR NOT NULL
- IsActive BOOLEAN NOT NULL
- CreatedAt TIMESTAMP NOT NULL
- UpdatedAt TIMESTAMP NULL

Constraint:
- Store: UNIQUE(TenantId, Code)

------------------------------------------------------------
4.2 Usuários, Roles e Permissões
------------------------------------------------------------

User
- Id UUID PK
- FullName VARCHAR NOT NULL
- Email VARCHAR NOT NULL
- NormalizedEmail VARCHAR NOT NULL UNIQUE
- Phone VARCHAR NULL
- PasswordHash TEXT NOT NULL
- IsActive BOOLEAN NOT NULL
- EmailConfirmed BOOLEAN NOT NULL
- LastLoginAt TIMESTAMP NULL
- CreatedAt TIMESTAMP NOT NULL
- UpdatedAt TIMESTAMP NULL

UserTenant
- Id UUID PK
- UserId UUID FK User(Id)
- TenantId UUID FK Tenant(Id)
- IsActive BOOLEAN NOT NULL
- CreatedAt TIMESTAMP NOT NULL
- UpdatedAt TIMESTAMP NULL

Constraint:
- UNIQUE(UserId, TenantId)

UserStore
- Id UUID PK
- UserId UUID FK User(Id)
- TenantId UUID FK Tenant(Id)
- StoreId UUID FK Store(Id)
- IsActive BOOLEAN NOT NULL
- CreatedAt TIMESTAMP NOT NULL
- UpdatedAt TIMESTAMP NULL

Constraint:
- UNIQUE(UserId, StoreId)

Role
- Id UUID PK
- TenantId UUID NULL FK Tenant(Id)
- Name VARCHAR NOT NULL
- NormalizedName VARCHAR NOT NULL
- Description VARCHAR NULL
- Scope VARCHAR NOT NULL
- IsSystemRole BOOLEAN NOT NULL
- IsActive BOOLEAN NOT NULL
- CreatedAt TIMESTAMP NOT NULL
- UpdatedAt TIMESTAMP NULL

Role scopes:
- System
- Tenant
- Store

Exemplos de roles:
- SuperAdmin, Scope System
- TenantAdmin, Scope Tenant
- StoreManager, Scope Store
- StockOperator, Scope Store
- Seller, Scope Store
- Viewer, Scope Store

UserRole
- Id UUID PK
- UserId UUID FK User(Id)
- RoleId UUID FK Role(Id)
- TenantId UUID NULL FK Tenant(Id)
- StoreId UUID NULL FK Store(Id)
- CreatedAt TIMESTAMP NOT NULL

Regras:
- Role Scope System: TenantId e StoreId devem ser NULL
- Role Scope Tenant: TenantId obrigatório e StoreId NULL
- Role Scope Store: TenantId obrigatório e StoreId obrigatório

Permission
- Id UUID PK
- Name VARCHAR NOT NULL UNIQUE
- Description VARCHAR NULL
- Module VARCHAR NOT NULL

RolePermission
- RoleId UUID FK Role(Id)
- PermissionId UUID FK Permission(Id)

PK composta:
- RoleId + PermissionId

Permissões iniciais:
- tenants.view
- tenants.create
- tenants.edit
- tenants.disable
- tenants.setup
- stores.view
- stores.create
- stores.edit
- stores.disable
- users.view
- users.create
- users.edit
- users.disable
- users.assign_roles
- users.assign_stores
- roles.view
- roles.manage
- categories.manage
- product_types.manage
- attributes.manage
- products.view
- products.create
- products.edit
- products.disable
- inventory.view
- inventory.entry
- inventory.exit
- inventory.adjust
- inventory.transfer
- reports.view
- reports.export
- audit.view
- api_keys.manage

------------------------------------------------------------
4.3 Catálogo
------------------------------------------------------------

Category
- Id UUID PK
- TenantId UUID FK Tenant(Id)
- ParentCategoryId UUID NULL FK Category(Id)
- Name VARCHAR NOT NULL
- Slug VARCHAR NOT NULL
- Description TEXT NULL
- SortOrder INT NOT NULL
- IsActive BOOLEAN NOT NULL
- CreatedAt TIMESTAMP NOT NULL
- UpdatedAt TIMESTAMP NULL

ProductType
- Id UUID PK
- TenantId UUID FK Tenant(Id)
- Name VARCHAR NOT NULL
- Description TEXT NULL
- IsActive BOOLEAN NOT NULL
- CreatedAt TIMESTAMP NOT NULL
- UpdatedAt TIMESTAMP NULL

ProductAttribute
- Id UUID PK
- TenantId UUID FK Tenant(Id)
- Name VARCHAR NOT NULL
- Code VARCHAR NOT NULL
- Description TEXT NULL
- AttributeType VARCHAR NOT NULL
- IsVariantAttribute BOOLEAN NOT NULL
- IsRequired BOOLEAN NOT NULL
- SortOrder INT NOT NULL
- IsActive BOOLEAN NOT NULL
- CreatedAt TIMESTAMP NOT NULL
- UpdatedAt TIMESTAMP NULL

AttributeType:
- Select
- MultiSelect
- Text
- Number
- Boolean
- Date
- Decimal

ProductAttributeValue
- Id UUID PK
- TenantId UUID FK Tenant(Id)
- ProductAttributeId UUID FK ProductAttribute(Id)
- Name VARCHAR NOT NULL
- Code VARCHAR NOT NULL
- Value VARCHAR NULL
- HexCode VARCHAR NULL
- SortOrder INT NOT NULL
- IsActive BOOLEAN NOT NULL
- CreatedAt TIMESTAMP NOT NULL
- UpdatedAt TIMESTAMP NULL

ProductTypeAttribute
- Id UUID PK
- ProductTypeId UUID FK ProductType(Id)
- ProductAttributeId UUID FK ProductAttribute(Id)
- IsRequired BOOLEAN NOT NULL
- IsVariantAttribute BOOLEAN NOT NULL
- SortOrder INT NOT NULL

Product
- Id UUID PK
- TenantId UUID FK Tenant(Id)
- CategoryId UUID NULL FK Category(Id)
- ProductTypeId UUID NULL FK ProductType(Id)
- Name VARCHAR NOT NULL
- Slug VARCHAR NOT NULL
- Description TEXT NULL
- BaseSku VARCHAR NOT NULL
- Barcode VARCHAR NULL
- Brand VARCHAR NULL
- Status VARCHAR NOT NULL
- CreatedAt TIMESTAMP NOT NULL
- UpdatedAt TIMESTAMP NULL

Constraint:
- UNIQUE(TenantId, BaseSku)

ProductAttributeAssignment
- Id UUID PK
- ProductId UUID FK Product(Id)
- ProductAttributeId UUID FK ProductAttribute(Id)
- ProductAttributeValueId UUID NULL FK ProductAttributeValue(Id)
- TextValue TEXT NULL
- NumberValue INT NULL
- DecimalValue NUMERIC(18,2) NULL
- BooleanValue BOOLEAN NULL
- DateValue TIMESTAMP NULL

ProductVariantOption
- Id UUID PK
- ProductId UUID FK Product(Id)
- ProductAttributeId UUID FK ProductAttribute(Id)
- ProductAttributeValueId UUID FK ProductAttributeValue(Id)

ProductVariant
- Id UUID PK
- ProductId UUID FK Product(Id)
- Sku VARCHAR NOT NULL
- Barcode VARCHAR NULL
- Name VARCHAR NOT NULL
- DefaultSalePrice NUMERIC(18,2) NOT NULL
- DefaultCostPrice NUMERIC(18,2) NULL
- Status VARCHAR NOT NULL
- CreatedAt TIMESTAMP NOT NULL
- UpdatedAt TIMESTAMP NULL

Constraint:
- UNIQUE(Sku)

ProductVariantAttributeValue
- Id UUID PK
- ProductVariantId UUID FK ProductVariant(Id)
- ProductAttributeId UUID FK ProductAttribute(Id)
- ProductAttributeValueId UUID FK ProductAttributeValue(Id)

ProductImage
- Id UUID PK
- ProductId UUID FK Product(Id)
- ProductVariantId UUID NULL FK ProductVariant(Id)
- FileName VARCHAR NOT NULL
- OriginalFileName VARCHAR NOT NULL
- ContentType VARCHAR NOT NULL
- StorageProvider VARCHAR NOT NULL
- StoragePath TEXT NOT NULL
- PublicUrl TEXT NULL
- AltText VARCHAR NULL
- SortOrder INT NOT NULL
- IsMain BOOLEAN NOT NULL
- CreatedAt TIMESTAMP NOT NULL
- UpdatedAt TIMESTAMP NULL

StorageProvider:
- Local
- S3

StoreProductVariant
- Id UUID PK
- StoreId UUID FK Store(Id)
- ProductVariantId UUID FK ProductVariant(Id)
- SalePrice NUMERIC(18,2) NULL
- CostPrice NUMERIC(18,2) NULL
- IsAvailable BOOLEAN NOT NULL
- CreatedAt TIMESTAMP NOT NULL
- UpdatedAt TIMESTAMP NULL

Constraint:
- UNIQUE(StoreId, ProductVariantId)

------------------------------------------------------------
4.4 Estoque
------------------------------------------------------------

StockLocation
- Id UUID PK
- StoreId UUID FK Store(Id)
- Name VARCHAR NOT NULL
- Type VARCHAR NOT NULL
- IsActive BOOLEAN NOT NULL
- CreatedAt TIMESTAMP NOT NULL
- UpdatedAt TIMESTAMP NULL

InventoryBalance
- Id UUID PK
- StockLocationId UUID FK StockLocation(Id)
- ProductVariantId UUID FK ProductVariant(Id)
- Quantity INT NOT NULL
- ReservedQuantity INT NOT NULL
- MinimumStock INT NOT NULL
- UpdatedAt TIMESTAMP NULL

Constraint:
- UNIQUE(StockLocationId, ProductVariantId)

InventoryBatch
- Id UUID PK
- StockLocationId UUID FK StockLocation(Id)
- ProductVariantId UUID FK ProductVariant(Id)
- BatchNumber VARCHAR NULL
- ExpirationDate TIMESTAMP NULL
- Quantity INT NOT NULL
- CreatedAt TIMESTAMP NOT NULL
- UpdatedAt TIMESTAMP NULL

StockMovement
- Id UUID PK
- TenantId UUID FK Tenant(Id)
- StoreId UUID FK Store(Id)
- StockLocationId UUID FK StockLocation(Id)
- ProductVariantId UUID FK ProductVariant(Id)
- Type VARCHAR NOT NULL
- Quantity INT NOT NULL
- UnitCost NUMERIC(18,2) NULL
- Reason VARCHAR NULL
- Notes TEXT NULL
- UserId UUID NULL FK User(Id)
- CreatedAt TIMESTAMP NOT NULL

Tipos de movimento:
- Purchase
- Sale
- Return
- Adjustment
- TransferIn
- TransferOut
- Loss
- Production
- Consumption

------------------------------------------------------------
4.5 Integrações externas
------------------------------------------------------------

ApiClient
- Id UUID PK
- TenantId UUID FK Tenant(Id)
- StoreId UUID NULL FK Store(Id)
- Name VARCHAR NOT NULL
- ApiKeyHash TEXT NOT NULL
- IsActive BOOLEAN NOT NULL
- CreatedAt TIMESTAMP NOT NULL
- UpdatedAt TIMESTAMP NULL
- LastUsedAt TIMESTAMP NULL

ApiClientPermission
- Id UUID PK
- ApiClientId UUID FK ApiClient(Id)
- Permission VARCHAR NOT NULL

Permissões sugeridas:
- catalog.read
- inventory.read
- orders.write

ExternalApiRequestLog
- Id UUID PK
- ApiClientId UUID NULL FK ApiClient(Id)
- TenantId UUID NULL FK Tenant(Id)
- StoreId UUID NULL FK Store(Id)
- Path TEXT NOT NULL
- Method VARCHAR NOT NULL
- StatusCode INT NOT NULL
- IpAddress VARCHAR NULL
- UserAgent TEXT NULL
- CreatedAt TIMESTAMP NOT NULL

------------------------------------------------------------
4.6 Auditoria
------------------------------------------------------------

AuditLog
- Id UUID PK
- UserId UUID NULL FK User(Id)
- TenantId UUID NULL FK Tenant(Id)
- StoreId UUID NULL FK Store(Id)
- Action VARCHAR NOT NULL
- EntityName VARCHAR NOT NULL
- EntityId UUID NULL
- OldValue TEXT NULL
- NewValue TEXT NULL
- IpAddress VARCHAR NULL
- UserAgent TEXT NULL
- CreatedAt TIMESTAMP NOT NULL

LoginAttempt
- Id UUID PK
- Email VARCHAR NOT NULL
- Success BOOLEAN NOT NULL
- FailureReason VARCHAR NULL
- IpAddress VARCHAR NULL
- UserAgent TEXT NULL
- CreatedAt TIMESTAMP NOT NULL

============================================================
5. AUTENTICAÇÃO E AUTORIZAÇÃO
============================================================

Para o painel administrativo:

- Usar Cookie Authentication.
- Usar Argon2id para hash de senha.
- Não usar EF Core Identity.
- Criar autenticação customizada usando os recursos do ASP.NET Core:
  - ClaimsPrincipal
  - CookieAuthenticationDefaults
  - Authorize
  - Policies
  - AntiForgeryToken quando aplicável

Criar interface:

IPasswordHasher
- HashPassword(string password)
- VerifyPassword(string password, string passwordHash)

Implementar com Argon2id.

Usar parâmetros seguros:
- Algorithm: Argon2id
- Memory: pelo menos 19 MiB
- Iterations: pelo menos 2
- Parallelism: 1
- Salt: 16 bytes
- Hash: 32 bytes

Salvar o hash em formato versionado, por exemplo:
argon2id$m=19456,t=2,p=1$saltBase64$hashBase64

Configurar cookie:
- HttpOnly true
- Secure always em produção
- SameSite Lax ou Strict
- Expiração configurável
- Sliding expiration

Authorization:

A autorização deve considerar:
- usuário;
- tenant atual;
- loja atual;
- role;
- permissões;
- escopo.

Escopos:
- System
- Tenant
- Store

SuperAdmin:
- Scope System
- Pode gerenciar tenants, lojas, usuários, permissões e configurações globais.

TenantAdmin:
- Scope Tenant
- Pode gerenciar apenas o próprio tenant e lojas do tenant.
- Pode gerenciar usuários daquele tenant e definir lojas acessíveis.

StoreManager, StockOperator, Seller, Viewer:
- Scope Store
- Permissões limitadas às lojas vinculadas.

Criar um serviço de autorização de aplicação:

ICurrentUserService
- UserId
- IsAuthenticated
- IsSuperAdmin
- CurrentTenantId
- CurrentStoreId

IPermissionService
- HasPermissionAsync(userId, permission, tenantId, storeId)

ITenantAccessService
- UserHasAccessToTenantAsync(userId, tenantId)
- UserHasAccessToStoreAsync(userId, tenantId, storeId)

Nunca confiar apenas no frontend.
Toda query multi-tenant deve filtrar por TenantId e, quando necessário, StoreId.

============================================================
6. API EXTERNA
============================================================

Criar uma API externa para integração com sites/e-commerces.

Ela deve permitir inicialmente:

GET /api/external/catalog/categories
GET /api/external/catalog/products
GET /api/external/catalog/products/{id}
GET /api/external/catalog/products/{id}/variants
GET /api/external/catalog/variants/{id}
GET /api/external/catalog/variants/{id}/images
GET /api/external/inventory/availability?variantId={id}

A API externa deve:
- autenticar via API Key inicialmente;
- validar o hash da API Key no banco;
- associar a API Key a um Tenant e opcionalmente a uma Store;
- registrar logs em ExternalApiRequestLog;
- retornar apenas dados do tenant/store autorizado;
- nunca expor dados internos sensíveis;
- ter rate limit preparado, mesmo que inicialmente simples.

API Key:
- A chave real deve ser exibida apenas uma vez na criação.
- Salvar apenas hash da chave no banco.
- Criar permissões para API Client, como catalog.read e inventory.read.

============================================================
7. STORAGE DE IMAGENS
============================================================

Produtos e variações podem ter imagens.

Entidade:
ProductImage

Regras:
- Uma imagem pode pertencer ao Product.
- Uma imagem também pode pertencer a uma ProductVariant, quando necessário.
- Deve existir IsMain para imagem principal.
- Deve existir SortOrder para ordenação.
- Deve armazenar StorageProvider, StoragePath e PublicUrl quando aplicável.

Criar interface:

IFileStorageService
- Task<StoredFileResult> UploadAsync(FileUploadRequest request)
- Task DeleteAsync(string storagePath)
- Task<Stream> GetAsync(string storagePath)

Implementações:
- LocalFileStorageService para desenvolvimento.
- S3FileStorageService para produção.

Configuração:

Development:
- Storage:Provider = Local
- Storage:Local:BasePath = wwwroot/uploads

Production:
- Storage:Provider = S3
- Storage:S3:BucketName
- Storage:S3:Region
- Storage:S3:AccessKey
- Storage:S3:SecretKey
- Storage:S3:PublicBaseUrl

As imagens devem ser organizadas por tenant/produto:

Exemplo:
tenants/{tenantId}/products/{productId}/{imageId}.webp

Preferencialmente validar:
- tamanho máximo;
- extensão;
- content type;
- nomes seguros;
- evitar path traversal.

Se possível, preparar conversão futura para WebP, mas não precisa implementar de início se aumentar muito o escopo.

============================================================
8. WIZARD DE SETUP DE TENANT
============================================================

Criar um fluxo para SuperAdmin provisionar um tenant.

Etapas:

1. Empresa
Campos:
- Name
- LegalName
- Document
- Slug
- Email
- Phone
- MainSegment

2. Configurações
Campos:
- Currency
- TimeZone
- DefaultLanguage
- AllowMultipleStores
- AllowBatchControl
- AllowExpirationControl
- AllowStoreSpecificPricing

3. Lojas
Permitir adicionar uma ou mais lojas.
Campos:
- Name
- Code
- Document
- Email
- Phone
- AddressLine
- City
- State
- ZipCode
- Type

Ao criar uma loja, criar automaticamente:
- StockLocation padrão chamado "Estoque Principal"

4. Administrador
Campos:
- FullName
- Email
- Phone
- TemporaryPassword ou SendInviteEmail

Criar usuário com role TenantAdmin.

5. Catálogo inicial
Permitir escolher template:
- Fashion
- Shoes
- Electronics
- ReligiousGoods
- FoodBakery
- SnackBarRestaurant
- Market
- Custom

O template deve criar:
- ProductTypes
- ProductAttributes
- ProductAttributeValues
- ProductTypeAttributes
- Categories

6. Revisão
Mostrar resumo e confirmar.

Endpoint sugerido:
POST /api/admin/tenants/setup

O setup deve rodar em transação.
Se falhar, desfazer tudo.

Criar:
CreateTenantSetupCommand
TenantSetupService
CatalogTemplateSeeder
UserProvisioningService
AuditLogService

============================================================
9. GERAÇÃO AUTOMÁTICA DE VARIAÇÕES
============================================================

Quando o usuário cadastrar um produto, ele poderá selecionar atributos e valores que geram variações.

Exemplo:
Produto: Camiseta São Bento
BaseSku: CAM-SB

Atributos selecionados:
- Cor: Preto, Branco
- Tamanho: P, M, G
- Modelo: Oversized

O sistema deve gerar o produto cartesiano:
2 cores × 3 tamanhos × 1 modelo = 6 ProductVariants

Cada ProductVariant deve ter:
- Sku gerado
- Name gerado
- DefaultSalePrice
- DefaultCostPrice
- ProductVariantAttributeValue correspondente

SKU sugerido:
BaseSku + códigos dos valores dos atributos

Exemplo:
CAM-SB-PRE-P-OVER
CAM-SB-BRA-M-OVER

Criar serviço:

IProductVariantGenerator
- GenerateVariants(Product product, IEnumerable<SelectedVariantAttribute> selectedAttributes)

Regras:
- SKU deve ser único.
- Não duplicar variações já existentes.
- Permitir pré-visualização das variações no frontend antes de salvar.
- Permitir edição manual de preço, custo, status e barcode por variação.

============================================================
10. BOAS PRÁTICAS
============================================================

Código:
- Usar nomes em inglês.
- Usar async/await.
- Usar CancellationToken.
- Usar Result Pattern ou exceções controladas de aplicação.
- Separar DTOs de entidades.
- Não expor entidades diretamente na API.
- Validar entrada com FluentValidation ou validação equivalente.
- Usar transações nos casos críticos.
- Evitar lógica de negócio nos controllers.
- Controllers devem ser finos.
- Services/UseCases devem concentrar orquestração.
- Repositórios devem ser responsáveis apenas por persistência.

Segurança:
- Hash de senha com Argon2id.
- API keys salvas apenas como hash.
- Cookies seguros.
- Proteção contra CSRF no painel web.
- Validação de tenant/store em toda operação.
- Não permitir acesso cruzado entre tenants.
- Registrar AuditLog para ações críticas:
  - criação/edição de tenant;
  - criação/edição de loja;
  - criação/edição de usuário;
  - alteração de permissões;
  - ajustes de estoque;
  - criação de API key.

Performance:
- Criar índices adequados.
- Evitar SELECT *.
- Paginar listagens.
- Usar queries otimizadas com Dapper.
- Evitar N+1 queries.
- Usar projeções específicas para listagens.
- Preparar cache futuramente, mas não implementar se não for necessário.

Banco:
- Toda tabela principal deve ter CreatedAt e UpdatedAt quando fizer sentido.
- Usar UUID como PK.
- Usar constraints para evitar duplicidade.
- Usar índices em TenantId, StoreId, ProductId, Sku, Slug e NormalizedEmail.
- Todas as queries de dados do tenant devem filtrar por TenantId.

============================================================
11. PRIMEIRO ESCOPO A IMPLEMENTAR
============================================================

Implementar em etapas.

Etapa 1:
- Criar solution e estrutura de projetos.
- Configurar PostgreSQL.
- Configurar Dapper.
- Criar migrations iniciais.
- Criar entidades base.
- Criar conexão com banco.
- Criar health check simples.

Etapa 2:
- Implementar auth com Cookie Authentication.
- Implementar Argon2id.
- Criar usuário SuperAdmin seed.
- Criar login/logout.
- Criar ICurrentUserService.
- Criar PermissionService básico.

Etapa 3:
- Implementar Tenant, Store e wizard de setup.
- Criar TenantSettings.
- Criar StockLocation padrão.
- Criar TenantAdmin no setup.
- Criar templates iniciais de catálogo.

Etapa 4:
- Implementar catálogo:
  - Category
  - ProductType
  - ProductAttribute
  - ProductAttributeValue
  - Product
  - ProductVariant
  - ProductVariantGenerator

Etapa 5:
- Implementar imagens de produto:
  - ProductImage
  - LocalFileStorageService
  - S3FileStorageService
  - Upload/list/delete

Etapa 6:
- Implementar estoque:
  - StockLocation
  - InventoryBalance
  - StockMovement
  - Entrada
  - Saída
  - Ajuste

Etapa 7:
- Implementar API externa:
  - ApiClient
  - API Key
  - Catálogo público autorizado
  - Consulta de estoque disponível
  - Logs

============================================================
12. IMPORTANTE
============================================================

Antes de codar, analise a estrutura proposta e, se encontrar inconsistências ou melhorias claras, sugira ajustes pontuais, mas mantenha a ideia principal:

- SaaS multi-tenant;
- tenant com múltiplas lojas;
- catálogo genérico baseado em atributos configuráveis;
- variações geradas automaticamente;
- usuários com roles e permissões por escopo;
- React moderno no frontend;
- PostgreSQL + Dapper;
- Cookie Auth para painel;
- API Key/Bearer para API externa;
- imagens em storage local no dev e S3 em produção;
- Clean Architecture.

Não simplifique demais a arquitetura.
Não transforme o sistema em CRUD anêmico.
Não acople regras de negócio aos controllers.
Não use EF Core.
Não use ASP.NET Identity completo.
Não use JWT para o painel administrativo.
Use JWT/Bearer ou API Key apenas para a API externa, se necessário.