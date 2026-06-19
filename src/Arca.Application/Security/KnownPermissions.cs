namespace Arca.Application.Security;

public static class KnownPermissions
{
    public const string TenantsView = "tenants.view";
    public const string TenantsCreate = "tenants.create";
    public const string TenantsEdit = "tenants.edit";
    public const string TenantsDisable = "tenants.disable";
    public const string TenantsSetup = "tenants.setup";
    public const string StoresView = "stores.view";
    public const string StoresCreate = "stores.create";
    public const string StoresEdit = "stores.edit";
    public const string StoresDisable = "stores.disable";
    public const string UsersView = "users.view";
    public const string UsersCreate = "users.create";
    public const string UsersEdit = "users.edit";
    public const string UsersChangePassword = "users.change_password";
    public const string UsersDisable = "users.disable";
    public const string UsersAssignRoles = "users.assign_roles";
    public const string UsersAssignStores = "users.assign_stores";
    public const string RolesView = "roles.view";
    public const string RolesManage = "roles.manage";
    public const string CategoriesManage = "categories.manage";
    public const string ProductTypesManage = "product_types.manage";
    public const string AttributesManage = "attributes.manage";
    public const string ProductsView = "products.view";
    public const string ProductsCreate = "products.create";
    public const string ProductsEdit = "products.edit";
    public const string ProductsDisable = "products.disable";
    public const string InventoryView = "inventory.view";
    public const string InventoryEntry = "inventory.entry";
    public const string InventoryExit = "inventory.exit";
    public const string InventoryAdjust = "inventory.adjust";
    public const string InventoryTransfer = "inventory.transfer";
    public const string ReportsView = "reports.view";
    public const string ReportsExport = "reports.export";
    public const string AuditView = "audit.view";
    public const string ApiKeysManage = "api_keys.manage";

    public static readonly IReadOnlyList<(string Name, string Module, string Description)> All =
    [
        (TenantsView, "Tenants", "View tenants"),
        (TenantsCreate, "Tenants", "Create tenants"),
        (TenantsEdit, "Tenants", "Edit tenants"),
        (TenantsDisable, "Tenants", "Disable tenants"),
        (TenantsSetup, "Tenants", "Run tenant setup wizard"),
        (StoresView, "Stores", "View stores"),
        (StoresCreate, "Stores", "Create stores"),
        (StoresEdit, "Stores", "Edit stores"),
        (StoresDisable, "Stores", "Disable stores"),
        (UsersView, "Users", "View users"),
        (UsersCreate, "Users", "Create users"),
        (UsersEdit, "Users", "Edit users"),
        (UsersChangePassword, "Users", "Change user passwords"),
        (UsersDisable, "Users", "Disable users"),
        (UsersAssignRoles, "Users", "Assign roles to users"),
        (UsersAssignStores, "Users", "Assign stores to users"),
        (RolesView, "Roles", "View roles"),
        (RolesManage, "Roles", "Manage roles and permissions"),
        (CategoriesManage, "Catalog", "Manage categories"),
        (ProductTypesManage, "Catalog", "Manage product types"),
        (AttributesManage, "Catalog", "Manage product attributes"),
        (ProductsView, "Catalog", "View products"),
        (ProductsCreate, "Catalog", "Create products"),
        (ProductsEdit, "Catalog", "Edit products"),
        (ProductsDisable, "Catalog", "Disable products"),
        (InventoryView, "Inventory", "View inventory"),
        (InventoryEntry, "Inventory", "Register stock entries"),
        (InventoryExit, "Inventory", "Register stock exits"),
        (InventoryAdjust, "Inventory", "Adjust stock"),
        (InventoryTransfer, "Inventory", "Transfer stock"),
        (ReportsView, "Reports", "View reports"),
        (ReportsExport, "Reports", "Export reports"),
        (AuditView, "Audit", "View audit logs"),
        (ApiKeysManage, "Integrations", "Manage API keys")
    ];
}
