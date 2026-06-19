import { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import { api, setApiContext } from "../api";
import type { CurrentUserContext, StoreContext, TenantContext, UserAppContext } from "../types";

const tenantStorageKey = "arca.currentTenantId";
const storeStorageKey = "arca.currentStoreId";

type AppContextValue = {
  currentUser: CurrentUserContext | null;
  currentTenant: TenantContext | null;
  currentStore: StoreContext | null;
  availableTenants: TenantContext[];
  availableStores: StoreContext[];
  permissions: string[];
  isLoading: boolean;
  error: string | null;
  hasPermission: (permission: string) => boolean;
  selectTenant: (tenantId: string | null) => Promise<void>;
  selectStore: (storeId: string | null) => Promise<void>;
  reload: () => Promise<void>;
};

const AppContext = createContext<AppContextValue | null>(null);

export function AppContextProvider({ children }: { children: React.ReactNode }) {
  const [context, setContext] = useState<UserAppContext | null>(null);
  const [selectedTenantId, setSelectedTenantId] = useState<string | null>(
    () => localStorage.getItem(tenantStorageKey)
  );
  const [selectedStoreId, setSelectedStoreId] = useState<string | null>(
    () => localStorage.getItem(storeStorageKey)
  );
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const applyContext = useCallback((next: UserAppContext) => {
    setContext(next);

    const tenantId = next.currentTenant?.id ?? null;
    const storeId = next.currentStore?.id ?? null;

    setSelectedTenantId(tenantId);
    setSelectedStoreId(storeId);
    setApiContext({ tenantId, storeId });

    if (tenantId) localStorage.setItem(tenantStorageKey, tenantId);
    else localStorage.removeItem(tenantStorageKey);

    if (storeId) localStorage.setItem(storeStorageKey, storeId);
    else localStorage.removeItem(storeStorageKey);
  }, []);

  const load = useCallback(async (tenantId = selectedTenantId, storeId = selectedStoreId) => {
    setIsLoading(true);
    setError(null);
    setApiContext({ tenantId, storeId });

    try {
      const next = await api.context.get();
      applyContext(next);
    } catch (err: unknown) {
      if (tenantId || storeId) {
        localStorage.removeItem(tenantStorageKey);
        localStorage.removeItem(storeStorageKey);
        setSelectedTenantId(null);
        setSelectedStoreId(null);
        setApiContext({});

        try {
          const next = await api.context.get();
          applyContext(next);
          return;
        } catch {
          setApiContext({});
        }
      }

      setError(err instanceof Error ? err.message : "Could not load application context.");
      setApiContext({});
    } finally {
      setIsLoading(false);
    }
  }, [applyContext, selectedStoreId, selectedTenantId]);

  useEffect(() => {
    void load();
  }, []);

  const value = useMemo<AppContextValue>(() => ({
    currentUser: context?.currentUser ?? null,
    currentTenant: context?.currentTenant ?? null,
    currentStore: context?.currentStore ?? null,
    availableTenants: context?.availableTenants ?? [],
    availableStores: context?.availableStores ?? [],
    permissions: context?.currentUser.permissions ?? [],
    isLoading,
    error,
    hasPermission: (permission: string) =>
      context?.currentUser.isSuperAdmin === true
      || context?.currentUser.permissions.includes(permission) === true,
    selectTenant: async (tenantId: string | null) => {
      if (tenantId) localStorage.setItem(tenantStorageKey, tenantId);
      else localStorage.removeItem(tenantStorageKey);
      localStorage.removeItem(storeStorageKey);
      setSelectedTenantId(tenantId);
      setSelectedStoreId(null);
      await load(tenantId, null);
    },
    selectStore: async (storeId: string | null) => {
      if (storeId) localStorage.setItem(storeStorageKey, storeId);
      else localStorage.removeItem(storeStorageKey);
      setSelectedStoreId(storeId);
      await load(selectedTenantId, storeId);
    },
    reload: async () => load(),
  }), [context, error, isLoading, load, selectedTenantId]);

  return <AppContext.Provider value={value}>{children}</AppContext.Provider>;
}

export function useAppContext() {
  const context = useContext(AppContext);
  if (!context) {
    throw new Error("useAppContext must be used inside AppContextProvider.");
  }

  return context;
}
