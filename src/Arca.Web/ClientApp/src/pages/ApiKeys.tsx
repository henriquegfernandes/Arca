import { useState } from "react";
import { Field } from "../components/Field";
import { Toggle } from "../components/Toggle";
import { PaginationControls } from "../components/PaginationControls";
import { api } from "../api";
import type { ApiClient, Pagination } from "../types";
import { addRequired, addUuid } from "../utils/validation";

const externalApiPermissions = ["catalog.read", "inventory.read", "orders.write"];

export function ApiKeys() {
  const [draft, setDraft] = useState({ tenantId: "", storeId: "", name: "", permissions: ["catalog.read", "inventory.read"] });
  const [clients, setClients] = useState<ApiClient[]>([]);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [pagination, setPagination] = useState<Pagination | null>(null);
  const [createdKey, setCreatedKey] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [isBusy, setIsBusy] = useState(false);
  const [errors, setErrors] = useState<Record<string, string>>({});

  async function loadClients(nextPage = page) {
    const validationErrors: Record<string, string> = {};
    addRequired(validationErrors, "api.tenantId", draft.tenantId, "TenantId is required.");
    addUuid(validationErrors, "api.tenantId", draft.tenantId);
    setErrors(validationErrors);
    setMessage(null);
    if (Object.keys(validationErrors).length > 0) return;

    setIsBusy(true);
    try {
      const data = await api.apiClients.list(draft.tenantId.trim(), { page: nextPage, search });
      setClients(data.clients ?? []);
      setPagination(data.pagination ?? null);
      setPage(data.pagination?.page ?? nextPage);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : "Could not load API clients.");
    } finally {
      setIsBusy(false);
    }
  }

  async function createClient() {
    const validationErrors = validateClientDraft(draft);
    setErrors(validationErrors);
    setMessage(null);
    setCreatedKey(null);
    if (Object.keys(validationErrors).length > 0) return;

    setIsBusy(true);
    try {
      const data = await api.apiClients.create({
        tenantId: draft.tenantId.trim(),
        storeId: draft.storeId.trim() || null,
        name: draft.name.trim(),
        permissions: draft.permissions,
      });
      setCreatedKey(data.apiKey);
      setMessage("API client created. The key is shown once.");
      setDraft({ ...draft, name: "" });
      await loadClients(1);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : "Could not create API client.");
    } finally {
      setIsBusy(false);
    }
  }

  async function disableClient(apiClientId: string) {
    setIsBusy(true);
    setMessage(null);
    try {
      await api.apiClients.disable(apiClientId, draft.tenantId.trim());
      await loadClients(page);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : "Could not disable API client.");
    } finally {
      setIsBusy(false);
    }
  }

  function togglePermission(permission: string, checked: boolean) {
    const perms = checked ? [...draft.permissions, permission] : draft.permissions.filter((p) => p !== permission);
    setDraft({ ...draft, permissions: [...new Set(perms)] });
  }

  return (
    <section className="api-keys-panel">
      <div className="panel-section">
        <div className="form-grid">
          <Field label="TenantId" value={draft.tenantId} error={errors["api.tenantId"]} required onChange={(v) => setDraft({ ...draft, tenantId: v })} />
          <Field label="StoreId" value={draft.storeId} error={errors["api.storeId"]} onChange={(v) => setDraft({ ...draft, storeId: v })} />
          <Field label="Name" value={draft.name} error={errors["api.name"]} required onChange={(v) => setDraft({ ...draft, name: v })} />
          <Field label="Search" value={search} onChange={setSearch} />
        </div>

        <div className="permission-row">
          {externalApiPermissions.map((perm) => (
            <Toggle key={perm} label={perm} checked={draft.permissions.includes(perm)} onChange={(c) => togglePermission(perm, c)} />
          ))}
        </div>
        {errors["api.permissions"] && <div className="field-error">{errors["api.permissions"]}</div>}

        <div className="actions left">
          <button className="secondary" onClick={() => loadClients(1)} disabled={isBusy}>Load Clients</button>
          <button className="primary" onClick={createClient} disabled={isBusy}>Create API Key</button>
        </div>

        {createdKey && (
          <label className="field api-key-once">
            <span>API Key</span>
            <input value={createdKey} readOnly />
          </label>
        )}

        {message && <div className={message.startsWith("API client created") ? "notice success" : "notice error"}>{message}</div>}
      </div>

      <div className="table-shell">
        <table>
          <thead>
            <tr>
              <th>Name</th>
              <th>Permissions</th>
              <th>Status</th>
              <th>Last Used</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {clients.length === 0 ? (
              <tr><td colSpan={5}>No API clients loaded.</td></tr>
            ) : (
              clients.map((client) => (
                <tr key={client.id}>
                  <td>{client.name}</td>
                  <td>{client.permissions.join(", ")}</td>
                  <td>{client.isActive ? "Active" : "Disabled"}</td>
                  <td>{client.lastUsedAt ? new Date(client.lastUsedAt).toLocaleString() : "Never"}</td>
                  <td>{client.isActive && <button className="secondary" onClick={() => disableClient(client.id)} disabled={isBusy}>Disable</button>}</td>
                </tr>
              ))
            )}
          </tbody>
        </table>
        <PaginationControls pagination={pagination} onPageChange={loadClients} />
      </div>
    </section>
  );
}

function validateClientDraft(draft: { tenantId: string; storeId: string; name: string; permissions: string[] }) {
  const errors: Record<string, string> = {};
  addRequired(errors, "api.tenantId", draft.tenantId, "TenantId is required.");
  addUuid(errors, "api.tenantId", draft.tenantId);
  addUuid(errors, "api.storeId", draft.storeId);
  addRequired(errors, "api.name", draft.name, "Name is required.");
  if (draft.permissions.length === 0) errors["api.permissions"] = "Choose at least one permission.";
  return errors;
}
