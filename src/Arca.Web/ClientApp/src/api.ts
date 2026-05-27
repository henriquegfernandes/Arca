const csrfToken =
  document.querySelector<HTMLMetaElement>('meta[name="arca-csrf-token"]')?.content ?? "";

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
  tenants: {
    list: (options?: import("./types").PageOptions) =>
      request<{ tenants: import("./types").TenantSummary[]; pagination?: import("./types").Pagination }>(
        `/api/admin/tenants?${pageOptionsQuery(options)}`
      ),
    get: (id: string) =>
      request<import("./types").TenantSummary>(`/api/admin/tenants/${id}`),
    setup: (data: import("./types").TenantSetupDraft) =>
      request<{ tenantId: string }>("/api/admin/tenants/setup", {
        method: "POST",
        body: JSON.stringify(data),
      }),
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
    disable: (userId: string) =>
      request<void>(`/api/admin/users/${userId}`, { method: "DELETE" }),
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
      disable: (id: string, tenantId: string) =>
        request<void>(
          `/api/admin/catalog/product-types/${id}?tenantId=${tenantId}`,
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
      variants: {
        list: (productId: string, tenantId: string) =>
          request<{ variants: import("./types").ProductVariant[] }>(
            `/api/admin/catalog/products/${productId}/variants?tenantId=${tenantId}`
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
    disable: (id: string, tenantId: string) =>
      request<void>(
        `/api/admin/integrations/api-clients/${id}?tenantId=${tenantId}`,
        { method: "DELETE" }
      ),
  },
};
