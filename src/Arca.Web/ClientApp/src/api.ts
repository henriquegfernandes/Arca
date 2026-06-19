const csrfToken =
  document.querySelector<HTMLMetaElement>('meta[name="arca-csrf-token"]')?.content ?? "";

let requestContext: { tenantId?: string | null; storeId?: string | null } = {};

export function setApiContext(context: { tenantId?: string | null; storeId?: string | null }) {
  requestContext = context;
}

async function request<T>(url: string, options: RequestInit = {}): Promise<T> {
  const headers: Record<string, string> = {
    ...(options.headers as Record<string, string>),
  };

  if (csrfToken) {
    headers["RequestVerificationToken"] = csrfToken;
  }

  if (!(options.body instanceof FormData)) {
    headers["Content-Type"] = "application/json";
  }

  if (requestContext.tenantId) {
    headers["X-Tenant-Id"] = requestContext.tenantId;
  }

  if (requestContext.storeId) {
    headers["X-Store-Id"] = requestContext.storeId;
  }

  const response = await fetch(url, { ...options, headers });

  if (!response.ok) {
    const payload = await response.json().catch(() => null);
    throw new Error(payload?.error ?? `Request failed: ${response.status}`);
  }

  if (response.status === 204) return undefined as T;

  return response.json();
}

function pageQuery(tenantId: string, options: import("./types").PageOptions = {}) {
  return pageOptionsQuery(options, { tenantId });
}

function pageOptionsQuery(
  options: import("./types").PageOptions = {},
  seed: Record<string, string> = {}
) {
  const params = new URLSearchParams(seed);
  if (options.page) params.set("page", options.page.toString());
  if (options.pageSize) params.set("pageSize", options.pageSize.toString());
  if (options.search?.trim()) params.set("search", options.search.trim());
  return params.toString();
}

export const api = {
  context: {
    get: () => request<import("./types").UserAppContext>("/api/admin/context"),
  },

  dashboard: {
    summary: () => request<import("./types").DashboardSummary>("/api/admin/dashboard/summary"),
  },

  tenants: {
    list: (options?: import("./types").PageOptions) =>
      request<{ tenants: import("./types").TenantSummary[]; pagination?: import("./types").Pagination }>(
        `/api/admin/tenants?${pageOptionsQuery(options)}`
      ),
    get: (id: string) =>
      request<import("./types").TenantDetails>(`/api/admin/tenants/${id}`),
    setup: (data: import("./types").TenantSetupDraft) =>
      request<{ tenantId: string }>("/api/admin/tenants/setup", {
        method: "POST",
        body: JSON.stringify(data),
      }),
    update: (id: string, data: Record<string, unknown>) =>
      request<import("./types").TenantDetails>(`/api/admin/tenants/${id}`, {
        method: "PUT",
        body: JSON.stringify(data),
      }),
    disable: (id: string) =>
      request<void>(`/api/admin/tenants/${id}`, { method: "DELETE" }),
    activate: (id: string) =>
      request<void>(`/api/admin/tenants/${id}/activate`, { method: "POST" }),
    stores: {
      list: (tenantId: string, options?: import("./types").PageOptions) =>
        request<{ stores: import("./types").StoreSummary[]; pagination?: import("./types").Pagination }>(
          `/api/admin/tenants/${tenantId}/stores?${pageOptionsQuery(options)}`
        ),
      create: (tenantId: string, data: import("./types").StoreDraft) =>
        request<import("./types").StoreSummary>(
          `/api/admin/tenants/${tenantId}/stores`,
          { method: "POST", body: JSON.stringify(data) }
        ),
      update: (tenantId: string, storeId: string, data: import("./types").StoreDraft) =>
        request<import("./types").StoreSummary>(
          `/api/admin/tenants/${tenantId}/stores/${storeId}`,
          { method: "PUT", body: JSON.stringify(data) }
        ),
      disable: (tenantId: string, storeId: string) =>
        request<void>(`/api/admin/tenants/${tenantId}/stores/${storeId}`, {
          method: "DELETE",
        }),
      activate: (tenantId: string, storeId: string) =>
        request<void>(`/api/admin/tenants/${tenantId}/stores/${storeId}/activate`, {
          method: "POST",
        }),
    },
  },

  users: {
    list: (tenantId?: string, options?: import("./types").PageOptions) =>
      request<{ users: import("./types").UserSummary[]; pagination?: import("./types").Pagination }>(
        `/api/admin/users?${pageOptionsQuery(options, tenantId ? { tenantId } : {})}`
      ),
    roles: (tenantId?: string, options?: import("./types").PageOptions) =>
      request<{ roles: import("./types").RoleSummary[]; pagination?: import("./types").Pagination }>(
        `/api/admin/users/roles?${pageOptionsQuery(options, tenantId ? { tenantId } : {})}`
      ),
    create: (data: Record<string, unknown>) =>
      request<import("./types").UserSummary>("/api/admin/users", {
        method: "POST",
        body: JSON.stringify(data),
      }),
    update: (userId: string, data: Record<string, unknown>) =>
      request<import("./types").UserSummary>(`/api/admin/users/${userId}`, {
        method: "PUT",
        body: JSON.stringify(data),
      }),
    changePassword: (userId: string, data: Record<string, unknown>) =>
      request<void>(`/api/admin/users/${userId}/change-password`, {
        method: "POST",
        body: JSON.stringify(data),
      }),
    disable: (userId: string) =>
      request<void>(`/api/admin/users/${userId}`, { method: "DELETE" }),
    activate: (userId: string) =>
      request<void>(`/api/admin/users/${userId}/activate`, { method: "POST" }),
  },

  roles: {
    list: (tenantId?: string, options?: import("./types").PageOptions) =>
      request<{ roles: import("./types").RoleDetails[]; pagination?: import("./types").Pagination }>(
        `/api/admin/roles?${pageOptionsQuery(options, tenantId ? { tenantId } : {})}`
      ),
    permissions: () =>
      request<{ permissions: import("./types").Permission[] }>(
        "/api/admin/roles/permissions"
      ),
    create: (data: Record<string, unknown>) =>
      request<import("./types").RoleDetails>("/api/admin/roles", {
        method: "POST",
        body: JSON.stringify(data),
      }),
    updatePermissions: (roleId: string, permissions: string[]) =>
      request<import("./types").RoleDetails>(
        `/api/admin/roles/${roleId}/permissions`,
        { method: "PUT", body: JSON.stringify({ permissions }) }
      ),
    disable: (roleId: string) =>
      request<void>(`/api/admin/roles/${roleId}`, { method: "DELETE" }),
    activate: (roleId: string) =>
      request<void>(`/api/admin/roles/${roleId}/activate`, { method: "POST" }),
    delete: (roleId: string) =>
      request<void>(`/api/admin/roles/${roleId}/delete`, { method: "DELETE" }),
  },

  catalog: {
    categories: {
      list: (tenantId: string, options?: import("./types").PageOptions) =>
        request<{ categories: import("./types").Category[]; pagination?: import("./types").Pagination }>(
          `/api/admin/catalog/categories?${pageQuery(tenantId, options)}`
        ),
      create: (data: Record<string, unknown>) =>
        request<import("./types").Category>("/api/admin/catalog/categories", {
          method: "POST",
          body: JSON.stringify(data),
        }),
      update: (id: string, data: Record<string, unknown>) =>
        request<import("./types").Category>(
          `/api/admin/catalog/categories/${id}`,
          { method: "PUT", body: JSON.stringify(data) }
        ),
      disable: (id: string, tenantId: string) =>
        request<void>(
          `/api/admin/catalog/categories/${id}?tenantId=${tenantId}`,
          { method: "DELETE" }
        ),
      activate: (id: string, tenantId: string) =>
        request<void>(
          `/api/admin/catalog/categories/${id}/activate?tenantId=${tenantId}`,
          { method: "POST" }
        ),
      delete: (id: string, tenantId: string) =>
        request<void>(
          `/api/admin/catalog/categories/${id}/delete?tenantId=${tenantId}`,
          { method: "DELETE" }
        ),
    },
    productTypes: {
      list: (tenantId: string, options?: import("./types").PageOptions) =>
        request<{ productTypes: import("./types").ProductType[]; pagination?: import("./types").Pagination }>(
          `/api/admin/catalog/product-types?${pageQuery(tenantId, options)}`
        ),
      create: (data: Record<string, unknown>) =>
        request<import("./types").ProductType>(
          "/api/admin/catalog/product-types",
          { method: "POST", body: JSON.stringify(data) }
        ),
      update: (id: string, data: Record<string, unknown>) =>
        request<import("./types").ProductType>(
          `/api/admin/catalog/product-types/${id}`,
          { method: "PUT", body: JSON.stringify(data) }
        ),
      attributes: (id: string, tenantId: string) =>
        request<{ attributes: import("./types").ProductAttribute[] }>(
          `/api/admin/catalog/product-types/${id}/attributes?tenantId=${tenantId}`
        ),
      disable: (id: string, tenantId: string) =>
        request<void>(
          `/api/admin/catalog/product-types/${id}?tenantId=${tenantId}`,
          { method: "DELETE" }
        ),
      activate: (id: string, tenantId: string) =>
        request<void>(
          `/api/admin/catalog/product-types/${id}/activate?tenantId=${tenantId}`,
          { method: "POST" }
        ),
      delete: (id: string, tenantId: string) =>
        request<void>(
          `/api/admin/catalog/product-types/${id}/delete?tenantId=${tenantId}`,
          { method: "DELETE" }
        ),
    },
    attributes: {
      list: (tenantId: string, options?: import("./types").PageOptions) =>
        request<{ attributes: import("./types").ProductAttribute[]; pagination?: import("./types").Pagination }>(
          `/api/admin/catalog/attributes?${pageQuery(tenantId, options)}`
        ),
      create: (data: Record<string, unknown>) =>
        request<import("./types").ProductAttribute>(
          "/api/admin/catalog/attributes",
          { method: "POST", body: JSON.stringify(data) }
        ),
      update: (id: string, data: Record<string, unknown>) =>
        request<import("./types").ProductAttribute>(
          `/api/admin/catalog/attributes/${id}`,
          { method: "PUT", body: JSON.stringify(data) }
        ),
      disable: (id: string, tenantId: string) =>
        request<void>(
          `/api/admin/catalog/attributes/${id}?tenantId=${tenantId}`,
          { method: "DELETE" }
        ),
      activate: (id: string, tenantId: string) =>
        request<void>(
          `/api/admin/catalog/attributes/${id}/activate?tenantId=${tenantId}`,
          { method: "POST" }
        ),
      delete: (id: string, tenantId: string) =>
        request<void>(
          `/api/admin/catalog/attributes/${id}/delete?tenantId=${tenantId}`,
          { method: "DELETE" }
        ),
      values: {
        list: (attributeId: string, tenantId: string, options?: import("./types").PageOptions) =>
          request<{ values: import("./types").AttributeValue[]; pagination?: import("./types").Pagination }>(
            `/api/admin/catalog/attributes/${attributeId}/values?${pageQuery(tenantId, options)}`
          ),
        create: (attributeId: string, data: Record<string, unknown>) =>
          request<import("./types").AttributeValue>(
            `/api/admin/catalog/attributes/${attributeId}/values`,
            { method: "POST", body: JSON.stringify(data) }
          ),
        update: (
          attributeId: string,
          valueId: string,
          data: Record<string, unknown>
        ) =>
          request<import("./types").AttributeValue>(
            `/api/admin/catalog/attributes/${attributeId}/values/${valueId}`,
            { method: "PUT", body: JSON.stringify(data) }
          ),
        disable: (attributeId: string, valueId: string, tenantId: string) =>
          request<void>(
            `/api/admin/catalog/attributes/${attributeId}/values/${valueId}?tenantId=${tenantId}`,
            { method: "DELETE" }
          ),
        activate: (attributeId: string, valueId: string, tenantId: string) =>
          request<void>(
            `/api/admin/catalog/attributes/${attributeId}/values/${valueId}/activate?tenantId=${tenantId}`,
            { method: "POST" }
          ),
        delete: (attributeId: string, valueId: string, tenantId: string) =>
          request<void>(
            `/api/admin/catalog/attributes/${attributeId}/values/${valueId}/delete?tenantId=${tenantId}`,
            { method: "DELETE" }
          ),
      },
    },
    products: {
      list: (tenantId: string, options?: import("./types").PageOptions) =>
        request<{ products: import("./types").ProductSummary[]; pagination?: import("./types").Pagination }>(
          `/api/admin/catalog/products?${pageQuery(tenantId, options)}`
        ),
      get: (id: string, tenantId: string) =>
        request<import("./types").ProductDetails>(
          `/api/admin/catalog/products/${id}?tenantId=${tenantId}`
        ),
      create: (data: Record<string, unknown>) =>
        request<{ productId: string; variants: unknown[] }>(
          "/api/admin/catalog/products",
          { method: "POST", body: JSON.stringify(data) }
        ),
      update: (id: string, data: Record<string, unknown>) =>
        request<import("./types").ProductSummary>(
          `/api/admin/catalog/products/${id}`,
          { method: "PUT", body: JSON.stringify(data) }
        ),
      disable: (id: string, tenantId: string) =>
        request<void>(
          `/api/admin/catalog/products/${id}?tenantId=${tenantId}`,
          { method: "DELETE" }
        ),
      activate: (id: string, tenantId: string) =>
        request<void>(
          `/api/admin/catalog/products/${id}/activate?tenantId=${tenantId}`,
          { method: "POST" }
        ),
      delete: (id: string, tenantId: string) =>
        request<void>(
          `/api/admin/catalog/products/${id}/delete?tenantId=${tenantId}`,
          { method: "DELETE" }
        ),
      variants: {
        list: (productId: string, tenantId: string) =>
          request<{ variants: import("./types").ProductVariant[] }>(
            `/api/admin/catalog/products/${productId}/variants?tenantId=${tenantId}`
          ),
        update: (productId: string, data: Record<string, unknown>) =>
          request<{ variants: import("./types").ProductVariant[] }>(
            `/api/admin/catalog/products/${productId}/variants`,
            { method: "PUT", body: JSON.stringify(data) }
          ),
        addGenerated: (productId: string, data: Record<string, unknown>) =>
          request<{ productId: string; variants: unknown[] }>(
            `/api/admin/catalog/products/${productId}/variants`,
            { method: "POST", body: JSON.stringify(data) }
          ),
        delete: (productId: string, variantId: string, tenantId: string) =>
          request<void>(
            `/api/admin/catalog/products/${productId}/variants/${variantId}?tenantId=${tenantId}`,
            { method: "DELETE" }
          ),
        preview: (data: Record<string, unknown>) =>
          request<{ variants: unknown[] }>(
            "/api/admin/catalog/variants/preview",
            { method: "POST", body: JSON.stringify(data) }
          ),
      },
      images: {
        list: (productId: string, tenantId: string) =>
          request<{ images: import("./types").ProductImage[] }>(
            `/api/admin/catalog/products/${productId}/images?tenantId=${tenantId}`
          ),
        upload: (productId: string, formData: FormData) =>
          request<{ image: import("./types").ProductImage }>(`/api/admin/catalog/products/${productId}/images`, {
            method: "POST",
            body: formData,
          }),
        delete: (productId: string, imageId: string, tenantId: string) =>
          request<void>(
            `/api/admin/catalog/products/${productId}/images/${imageId}?tenantId=${tenantId}`,
            { method: "DELETE" }
          ),
      },
    },
  },

  inventory: {
    products: (
      tenantId: string,
      storeId: string,
      options?: import("./types").PageOptions & {
        categoryId?: string;
        status?: string;
        lowStockOnly?: boolean;
        outOfStockOnly?: boolean;
        stockLocationId?: string;
      }
    ) => {
      const params = new URLSearchParams({ tenantId, storeId });
      if (options?.page) params.set("page", options.page.toString());
      if (options?.pageSize) params.set("pageSize", options.pageSize.toString());
      if (options?.search?.trim()) params.set("search", options.search.trim());
      if (options?.categoryId) params.set("categoryId", options.categoryId);
      if (options?.status) params.set("status", options.status);
      if (options?.lowStockOnly) params.set("lowStockOnly", "true");
      if (options?.outOfStockOnly) params.set("outOfStockOnly", "true");
      if (options?.stockLocationId) params.set("stockLocationId", options.stockLocationId);
      return request<{ products: import("./types").InventoryProductSummary[]; pagination?: import("./types").Pagination }>(
        `/api/admin/inventory/products?${params.toString()}`
      );
    },
    productDetails: (tenantId: string, storeId: string, productId: string, stockLocationId?: string) =>
      request<import("./types").InventoryProductDetails>(
        `/api/admin/inventory/products/${productId}?tenantId=${tenantId}&storeId=${storeId}${stockLocationId ? `&stockLocationId=${stockLocationId}` : ""}`
      ),
    balance: (
      tenantId: string,
      storeId: string,
      stockLocationId: string,
      productVariantId: string
    ) =>
      request<import("./types").InventoryBalance>(
        `/api/admin/inventory/balance?tenantId=${tenantId}&storeId=${storeId}&stockLocationId=${stockLocationId}&productVariantId=${productVariantId}`
      ),
    movements: (
      tenantId: string,
      storeId: string,
      productVariantId?: string,
      limit?: number
    ) =>
      request<{ movements: import("./types").StockMovement[] }>(
        `/api/admin/inventory/movements?tenantId=${tenantId}&storeId=${storeId}${
          productVariantId ? `&productVariantId=${productVariantId}` : ""
        }${limit ? `&limit=${limit}` : ""}`
      ),
    stockLocations: (tenantId: string, storeId: string) =>
      request<{ stockLocations: import("./types").StockLocation[] }>(
        `/api/admin/inventory/stock-locations?tenantId=${tenantId}&storeId=${storeId}`
      ),
    entry: (data: Record<string, unknown>) =>
      request<{ balance: import("./types").InventoryBalance; movement: import("./types").StockMovement }>(
        "/api/admin/inventory/entries",
        { method: "POST", body: JSON.stringify(data) }
      ),
    exit: (data: Record<string, unknown>) =>
      request<{ balance: import("./types").InventoryBalance; movement: import("./types").StockMovement }>(
        "/api/admin/inventory/exits",
        { method: "POST", body: JSON.stringify(data) }
      ),
    adjust: (data: Record<string, unknown>) =>
      request<{ balance: import("./types").InventoryBalance; movement: import("./types").StockMovement }>(
        "/api/admin/inventory/adjustments",
        { method: "POST", body: JSON.stringify(data) }
      ),
    movement: (data: Record<string, unknown>) =>
      request<{ movements: Array<{ balance: import("./types").InventoryBalance; movement: import("./types").StockMovement }> }>(
        "/api/admin/inventory/movements",
        { method: "POST", body: JSON.stringify(data) }
      ),
  },

  auditLogs: {
    list: (options?: import("./types").PageOptions & {
      tenantId?: string;
      storeId?: string;
      userId?: string;
      entityName?: string;
      action?: string;
      dateFrom?: string;
      dateTo?: string;
    }) => {
      const params = new URLSearchParams();
      if (options?.tenantId) params.set("tenantId", options.tenantId);
      if (options?.storeId) params.set("storeId", options.storeId);
      if (options?.userId) params.set("userId", options.userId);
      if (options?.entityName) params.set("entityName", options.entityName);
      if (options?.action) params.set("action", options.action);
      if (options?.dateFrom) params.set("dateFrom", options.dateFrom);
      if (options?.dateTo) params.set("dateTo", options.dateTo);
      if (options?.page) params.set("page", options.page.toString());
      if (options?.pageSize) params.set("pageSize", options.pageSize.toString());
      if (options?.search?.trim()) params.set("search", options.search.trim());
      return request<{ logs: import("./types").AuditLogEntry[]; pagination?: import("./types").Pagination }>(
        `/api/admin/audit-logs?${params.toString()}`
      );
    },
  },

  apiClients: {
    list: (tenantId: string, options?: import("./types").PageOptions) =>
      request<{ clients: import("./types").ApiClient[]; pagination?: import("./types").Pagination }>(
        `/api/admin/integrations/api-clients?${pageQuery(tenantId, options)}`
      ),
    create: (data: Record<string, unknown>) =>
      request<{ id: string; apiKey: string }>(
        "/api/admin/integrations/api-clients",
        { method: "POST", body: JSON.stringify(data) }
      ),
    update: (id: string, data: Record<string, unknown>) =>
      request<import("./types").ApiClient>(
        `/api/admin/integrations/api-clients/${id}`,
        { method: "PUT", body: JSON.stringify(data) }
      ),
    disable: (id: string, tenantId: string) =>
      request<void>(
        `/api/admin/integrations/api-clients/${id}?tenantId=${tenantId}`,
        { method: "DELETE" }
      ),
    delete: (id: string, tenantId: string) =>
      request<void>(
        `/api/admin/integrations/api-clients/${id}/delete?tenantId=${tenantId}`,
        { method: "DELETE" }
      ),
  },
};
