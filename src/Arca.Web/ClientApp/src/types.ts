export type StoreDraft = {
  name: string;
  code: string;
  document: string;
  email: string;
  phone: string;
  addressLine: string;
  city: string;
  state: string;
  zipCode: string;
  type: string;
};

export type TenantSetupDraft = {
  company: {
    name: string;
    legalName: string;
    document: string;
    slug: string;
    email: string;
    phone: string;
    mainSegment: string;
  };
  settings: {
    currency: string;
    timeZone: string;
    defaultLanguage: string;
    allowMultipleStores: boolean;
    allowBatchControl: boolean;
    allowExpirationControl: boolean;
    allowStoreSpecificPricing: boolean;
  };
  stores: StoreDraft[];
  administrator: {
    fullName: string;
    email: string;
    phone: string;
    temporaryPassword: string;
    sendInviteEmail: boolean;
  };
  catalog: {
    template: string;
  };
};

export type ValidationErrors = Record<string, string>;

export type PageOptions = {
  page?: number;
  pageSize?: number;
  search?: string;
};

export type Pagination = {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
};

export type ApiClient = {
  id: string;
  tenantId: string;
  storeId: string | null;
  name: string;
  isActive: boolean;
  permissions: string[];
  createdAt: string;
  lastUsedAt: string | null;
};

export type ApiClientDraft = {
  tenantId: string;
  storeId: string;
  name: string;
  permissions: string[];
};

export type TenantSummary = {
  id: string;
  name: string;
  slug: string;
  contactEmail: string | null;
  mainSegment: string | null;
  isActive: boolean;
  setupStatus: string;
  currency: string;
  timeZone: string;
  storeCount: number;
  createdAt: string;
};

export type StoreSummary = {
  id: string;
  tenantId: string;
  name: string;
  code: string;
  type: string;
  document: string | null;
  phone: string | null;
  email: string | null;
  addressLine: string | null;
  city: string | null;
  state: string | null;
  zipCode: string | null;
  isActive: boolean;
  createdAt: string;
};

export type UserSummary = {
  id: string;
  fullName: string;
  email: string;
  phone: string | null;
  isActive: boolean;
  emailConfirmed: boolean;
  lastLoginAt: string | null;
  createdAt: string;
  roles: UserRoleAssignment[];
};

export type UserRoleAssignment = {
  roleId: string;
  roleName: string;
  scope: string;
  tenantId: string | null;
  storeId: string | null;
};

export type RoleSummary = {
  id: string;
  tenantId: string | null;
  name: string;
  scope: string;
  isSystemRole: boolean;
  isActive: boolean;
};

export type Permission = {
  id: string;
  name: string;
  description: string;
  module: string;
};

export type RoleDetails = {
  id: string;
  tenantId: string | null;
  name: string;
  normalizedName: string;
  description: string | null;
  scope: string;
  isSystemRole: boolean;
  isActive: boolean;
  permissions: string[];
  createdAt: string;
  updatedAt: string | null;
};

export type RoleDraft = {
  tenantId: string;
  name: string;
  description: string;
  scope: string;
  permissions: string[];
};

export type UserDraft = {
  fullName: string;
  email: string;
  phone: string;
  temporaryPassword: string;
  roleId: string;
  tenantId: string;
  storeId: string;
};

export type Category = {
  id: string;
  tenantId: string;
  parentCategoryId: string | null;
  name: string;
  slug: string;
  description: string | null;
  sortOrder: number;
  isActive: boolean;
  createdAt: string;
};

export type ProductType = {
  id: string;
  tenantId: string;
  name: string;
  description: string | null;
  isActive: boolean;
  createdAt: string;
};

export type ProductAttribute = {
  id: string;
  tenantId: string;
  name: string;
  code: string;
  description: string | null;
  attributeType: string;
  isVariantAttribute: boolean;
  isRequired: boolean;
  sortOrder: number;
  isActive: boolean;
  createdAt: string;
};

export type AttributeValue = {
  id: string;
  tenantId: string;
  productAttributeId: string;
  name: string;
  code: string;
  value: string | null;
  hexCode: string | null;
  sortOrder: number;
  isActive: boolean;
  createdAt: string;
};

export type ProductSummary = {
  id: string;
  tenantId: string;
  categoryId: string | null;
  productTypeId: string | null;
  name: string;
  slug: string;
  description: string | null;
  baseSku: string;
  barcode: string | null;
  brand: string | null;
  status: string;
  variantCount: number;
  createdAt: string;
};

export type ProductDetails = {
  id: string;
  tenantId: string;
  categoryId: string | null;
  categoryName: string | null;
  productTypeId: string | null;
  productTypeName: string | null;
  name: string;
  slug: string;
  description: string | null;
  baseSku: string;
  barcode: string | null;
  brand: string | null;
  status: string;
  createdAt: string;
  updatedAt: string | null;
};

export type ProductVariant = {
  id: string;
  productId: string;
  sku: string;
  barcode: string | null;
  name: string;
  defaultSalePrice: number;
  defaultCostPrice: number | null;
  status: string;
  createdAt: string;
};

export type ProductImage = {
  id: string;
  productId: string;
  productVariantId: string | null;
  fileName: string;
  originalFileName: string;
  contentType: string;
  storageProvider: string;
  storagePath: string;
  publicUrl: string | null;
  altText: string | null;
  sortOrder: number;
  isMain: boolean;
  createdAt: string;
};

export type StockLocation = {
  id: string;
  storeId: string;
  name: string;
  type: string;
  isActive: boolean;
  createdAt: string;
};

export type InventoryBalance = {
  id: string;
  stockLocationId: string;
  productVariantId: string;
  quantity: number;
  reservedQuantity: number;
  availableQuantity: number;
  minimumStock: number;
  updatedAt: string | null;
};

export type StockMovement = {
  id: string;
  tenantId: string;
  storeId: string;
  stockLocationId: string;
  productVariantId: string;
  type: string;
  quantity: number;
  unitCost: number | null;
  reason: string | null;
  notes: string | null;
  userId: string | null;
  createdAt: string;
};
