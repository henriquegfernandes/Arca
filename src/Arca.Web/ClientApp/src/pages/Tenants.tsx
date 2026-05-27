import { useState } from "react";
import { TenantSetupWizard } from "../components/TenantSetupWizard";
import { Field } from "../components/Field";
import { PaginationControls } from "../components/PaginationControls";
import { api } from "../api";
import type { TenantSummary, StoreSummary, Pagination } from "../types";

export function Tenants() {
  const [tenants, setTenants] = useState<TenantSummary[]>([]);
  const [selectedTenantId, setSelectedTenantId] = useState("");
  const [stores, setStores] = useState<StoreSummary[]>([]);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [pagination, setPagination] = useState<Pagination | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  async function loadTenants(nextPage = page) {
    setIsLoading(true);
    setMessage(null);
    try {
      const data = await api.tenants.list({ page: nextPage, search });
      const loadedTenants = data.tenants ?? [];
      setTenants(loadedTenants);
      setPagination(data.pagination ?? null);
      setPage(data.pagination?.page ?? nextPage);
      if (loadedTenants.length > 0 && !selectedTenantId) {
        setSelectedTenantId(loadedTenants[0].id);
        await loadStores(loadedTenants[0].id);
      }
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : "Could not load tenants.");
    } finally {
      setIsLoading(false);
    }
  }

  async function loadStores(tenantId: string) {
    if (!tenantId) { setStores([]); return; }
    try {
      const data = await api.tenants.stores.list(tenantId, { pageSize: 8 });
      setStores(data.stores ?? []);
    } catch {
      setStores([]);
    }
  }

  async function selectTenant(tenantId: string) {
    setSelectedTenantId(tenantId);
    setMessage(null);
    await loadStores(tenantId);
  }

  return (
    <section className="tenants-panel">
      <div className="panel-section">
        <div className="section-heading">
          <div>
            <h2>Tenants</h2>
            <p>Provisioned companies and their stores.</p>
          </div>
          <button className="secondary" onClick={() => loadTenants(1)} disabled={isLoading}>
            {isLoading ? "Loading..." : "Refresh"}
          </button>
        </div>
        <div className="form-grid compact">
          <Field label="Search" value={search} onChange={setSearch} />
          <div className="field action-field">
            <span>&nbsp;</span>
            <button className="secondary" onClick={() => loadTenants(1)} disabled={isLoading}>
              Search
            </button>
          </div>
        </div>
        {message && <div className="notice error">{message}</div>}
        <div className="tenant-summary-grid">
          {tenants.length === 0 ? (
            <div className="empty-state">No tenants loaded.</div>
          ) : (
            tenants.map((tenant) => (
              <button
                key={tenant.id}
                className={tenant.id === selectedTenantId ? "tenant-summary selected" : "tenant-summary"}
                onClick={() => selectTenant(tenant.id)}
              >
                <strong>{tenant.name}</strong>
                <span>{tenant.slug}</span>
                <span>{tenant.storeCount} stores &middot; {tenant.currency}</span>
              </button>
            ))
          )}
        </div>
        <PaginationControls pagination={pagination} onPageChange={loadTenants} />
        {stores.length > 0 && (
          <div className="store-strip">
            {stores.map((store) => (
              <div key={store.id} className="store-chip">
                <strong>{store.name}</strong>
                <span>{store.code} &middot; {store.type}</span>
              </div>
            ))}
          </div>
        )}
      </div>
      <TenantSetupWizard />
    </section>
  );
}
