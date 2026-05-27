import { useState } from "react";
import { Field } from "../components/Field";
import { PaginationControls } from "../components/PaginationControls";
import { api } from "../api";
import type { Pagination, StoreSummary } from "../types";
import { normalizeStoreCode, addRequired, addUuid, addEmail, addPattern } from "../utils/validation";

const initialStore = {
  name: "",
  code: "",
  document: "",
  email: "",
  phone: "",
  addressLine: "",
  city: "",
  state: "",
  zipCode: "",
  type: "Physical",
};

export function Stores() {
  const [tenantId, setTenantId] = useState("");
  const [stores, setStores] = useState<StoreSummary[]>([]);
  const [selectedStoreId, setSelectedStoreId] = useState<string | null>(null);
  const [draft, setDraft] = useState(initialStore);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [pagination, setPagination] = useState<Pagination | null>(null);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [message, setMessage] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  async function loadStores(nextPage = page) {
    const validationErrors: Record<string, string> = {};
    addRequired(validationErrors, "storesPanel.tenantId", tenantId, "TenantId is required.");
    addUuid(validationErrors, "storesPanel.tenantId", tenantId);
    setErrors(validationErrors);
    setMessage(null);
    if (Object.keys(validationErrors).length > 0) return;

    setIsLoading(true);
    try {
      const data = await api.tenants.stores.list(tenantId.trim(), { page: nextPage, search });
      setStores(data.stores ?? []);
      setPagination(data.pagination ?? null);
      setPage(data.pagination?.page ?? nextPage);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : "Could not load stores.");
    } finally {
      setIsLoading(false);
    }
  }

  function editStore(store: StoreSummary) {
    setSelectedStoreId(store.id);
    setDraft({
      name: store.name,
      code: store.code,
      document: store.document ?? "",
      email: store.email ?? "",
      phone: store.phone ?? "",
      addressLine: store.addressLine ?? "",
      city: store.city ?? "",
      state: store.state ?? "",
      zipCode: store.zipCode ?? "",
      type: store.type,
    });
    setMessage(null);
    setErrors({});
  }

  function newStore() {
    setSelectedStoreId(null);
    setDraft({ ...initialStore, code: `STORE${stores.length + 1}` });
    setMessage(null);
    setErrors({});
  }

  async function saveStore() {
    const validationErrors = validateStoreDraft(tenantId, draft);
    setErrors(validationErrors);
    setMessage(null);
    if (Object.keys(validationErrors).length > 0) return;

    try {
      if (selectedStoreId) {
        await api.tenants.stores.update(tenantId.trim(), selectedStoreId, draft);
        setMessage("Store updated.");
      } else {
        await api.tenants.stores.create(tenantId.trim(), draft);
        setMessage("Store created.");
      }
      await loadStores(selectedStoreId ? page : 1);
      if (!selectedStoreId) newStore();
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : "Could not save store.");
    }
  }

  async function disableStore(storeId: string) {
    setMessage(null);
    try {
      await api.tenants.stores.disable(tenantId.trim(), storeId);
      setMessage("Store disabled.");
      await loadStores(page);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : "Could not disable store.");
    }
  }

  return (
    <section className="stores-panel">
      <div className="panel-section">
        <div className="section-heading">
          <div>
            <h2>Stores</h2>
            <p>Tenant stores.</p>
          </div>
          <button className="secondary" onClick={() => loadStores(1)} disabled={isLoading}>
            {isLoading ? "Loading..." : "Load Stores"}
          </button>
        </div>
        <div className="form-grid compact">
          <Field label="TenantId" value={tenantId} error={errors["storesPanel.tenantId"]} required onChange={setTenantId} />
          <Field label="Search" value={search} onChange={setSearch} />
        </div>
        {message && <div className="notice error">{message}</div>}
      </div>

      <div className="panel-section">
        <div className="section-heading">
          <div>
            <h2>{selectedStoreId ? "Edit Store" : "New Store"}</h2>
          </div>
          <button className="secondary" onClick={newStore}>New</button>
        </div>
        <div className="form-grid">
          <Field label="Store Name" value={draft.name} error={errors["store.name"]} required onChange={(v) => setDraft({ ...draft, name: v })} />
          <Field label="Code" value={draft.code} error={errors["store.code"]} required onChange={(v) => setDraft({ ...draft, code: normalizeStoreCode(v) })} />
          <Field label="Type" value={draft.type} error={errors["store.type"]} required onChange={(v) => setDraft({ ...draft, type: v })} />
          <Field label="Email" type="email" value={draft.email} error={errors["store.email"]} onChange={(v) => setDraft({ ...draft, email: v })} />
          <Field label="Phone" value={draft.phone} onChange={(v) => setDraft({ ...draft, phone: v })} />
          <Field label="Document" value={draft.document} onChange={(v) => setDraft({ ...draft, document: v })} />
          <Field label="Address" value={draft.addressLine} onChange={(v) => setDraft({ ...draft, addressLine: v })} />
          <Field label="City" value={draft.city} onChange={(v) => setDraft({ ...draft, city: v })} />
          <Field label="State" value={draft.state} onChange={(v) => setDraft({ ...draft, state: v })} />
          <Field label="Zip Code" value={draft.zipCode} onChange={(v) => setDraft({ ...draft, zipCode: v })} />
        </div>
        <div className="actions left">
          <button className="primary" onClick={saveStore}>{selectedStoreId ? "Update Store" : "Create Store"}</button>
        </div>
      </div>

      <div className="table-shell">
        <table>
          <thead>
            <tr>
              <th>Name</th>
              <th>Code</th>
              <th>Type</th>
              <th>Contact</th>
              <th>Status</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {stores.length === 0 ? (
              <tr><td colSpan={6}>No stores loaded.</td></tr>
            ) : (
              stores.map((store) => (
                <tr key={store.id}>
                  <td>{store.name}</td>
                  <td>{store.code}</td>
                  <td>{store.type}</td>
                  <td>{store.email || store.phone || "Not set"}</td>
                  <td>{store.isActive ? "Active" : "Disabled"}</td>
                  <td>
                    <div className="row-actions">
                      <button className="secondary" onClick={() => editStore(store)}>Edit</button>
                      {store.isActive && <button className="secondary" onClick={() => disableStore(store.id)}>Disable</button>}
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
        <PaginationControls pagination={pagination} onPageChange={loadStores} />
      </div>
    </section>
  );
}

function validateStoreDraft(tenantId: string, draft: typeof initialStore) {
  const errors: Record<string, string> = {};
  addRequired(errors, "storesPanel.tenantId", tenantId, "TenantId is required.");
  addUuid(errors, "storesPanel.tenantId", tenantId);
  addRequired(errors, "store.name", draft.name, "Store name is required.");
  addRequired(errors, "store.code", draft.code, "Store code is required.");
  addPattern(errors, "store.code", draft.code, /^[A-Z0-9]+(?:-[A-Z0-9]+)*$/, "Use uppercase letters, numbers and hyphens.");
  addRequired(errors, "store.type", draft.type, "Store type is required.");
  addEmail(errors, "store.email", draft.email);
  return errors;
}
