import { useEffect, useState } from "react";
import { CheckCircle2, Copy, Edit3, Eye, KeyRound, PowerOff, Trash2 } from "lucide-react";
import { ConfirmDialog, EntityModal, PageHeader, SearchInput } from "../components/Crud";
import { Field } from "../components/Field";
import { Toggle } from "../components/Toggle";
import { PaginationControls } from "../components/PaginationControls";
import { api } from "../api";
import { useAppContext } from "../context/AppContext";
import { useI18n } from "../i18n";
import type { ApiClient, Pagination, StoreSummary } from "../types";
import { addRequired } from "../utils/validation";

const externalApiPermissions = ["catalog.read", "inventory.read", "orders.write"];
const initialDraft = {
  name: "",
  storeId: "",
  isActive: true,
  permissions: ["catalog.read", "inventory.read"],
};

export function ApiKeys() {
  const { currentTenant, currentStore, availableStores } = useAppContext();
  const { t } = useI18n();
  const tenantId = currentTenant?.id ?? "";
  const [draft, setDraft] = useState(initialDraft);
  const [clients, setClients] = useState<ApiClient[]>([]);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [pagination, setPagination] = useState<Pagination | null>(null);
  const [createdKey, setCreatedKey] = useState<string | null>(null);
  const [createdKeyId, setCreatedKeyId] = useState<string | null>(null);
  const [stores, setStores] = useState<StoreSummary[]>([]);
  const [message, setMessage] = useState<string | null>(null);
  const [isBusy, setIsBusy] = useState(false);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [selectedClient, setSelectedClient] = useState<ApiClient | null>(null);
  const [clientToDisable, setClientToDisable] = useState<ApiClient | null>(null);
  const [clientToActivate, setClientToActivate] = useState<ApiClient | null>(null);
  const [clientToDelete, setClientToDelete] = useState<ApiClient | null>(null);
  const [credentialPreview, setCredentialPreview] = useState<{ title: string; value: string; warning?: string } | null>(null);

  useEffect(() => {
    if (!tenantId) {
      setClients([]);
      setPagination(null);
      return;
    }

    void loadClients(1);
    void loadStores();
  }, [tenantId]);

  async function loadStores() {
    if (!tenantId) {
      setStores([]);
      return;
    }

    try {
      const data = await api.tenants.stores.list(tenantId, { pageSize: 100 });
      setStores(data.stores ?? []);
    } catch {
      setStores([]);
    }
  }

  async function loadClients(nextPage = page) {
    setMessage(null);
    setErrors({});
    if (!tenantId) {
      setMessage(t("apiKeys.selectTenantLoad"));
      return;
    }

    setIsBusy(true);
    try {
      const data = await api.apiClients.list(tenantId, { page: nextPage, search });
      setClients(data.clients ?? []);
      setPagination(data.pagination ?? null);
      setPage(data.pagination?.page ?? nextPage);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("apiKeys.loadFailed"));
    } finally {
      setIsBusy(false);
    }
  }

  async function saveClient() {
    const validationErrors = validateClientDraft(draft);
    setErrors(validationErrors);
    setMessage(null);
    setCreatedKey(null);
    if (Object.keys(validationErrors).length > 0) return;
    if (!tenantId) {
      setMessage(t("apiKeys.selectTenantCreate"));
      return;
    }

    setIsBusy(true);
    try {
      const payload = {
        tenantId,
        storeId: draft.storeId || null,
        name: draft.name.trim(),
        isActive: draft.isActive,
        permissions: draft.permissions,
      };

      if (selectedClient) {
        await api.apiClients.update(selectedClient.id, payload);
        setMessage(t("apiKeys.updated"));
      } else {
        const data = await api.apiClients.create(payload);
        setCreatedKey(data.apiKey);
        setCreatedKeyId(data.id);
        setCredentialPreview({
          title: t("apiKeys.generatedCredential"),
          value: data.apiKey,
          warning: t("apiKeys.generatedCredentialWarning"),
        });
        setMessage(t("apiKeys.created"));
      }

      setDraft(initialDraft);
      setSelectedClient(null);
      setIsModalOpen(false);
      await loadClients(selectedClient ? page : 1);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("apiKeys.saveFailed"));
    } finally {
      setIsBusy(false);
    }
  }

  async function disableClient(apiClientId: string) {
    setIsBusy(true);
    setMessage(null);
    try {
      await api.apiClients.disable(apiClientId, tenantId);
      setMessage(t("apiKeys.disabled"));
      setClientToDisable(null);
      await loadClients(page);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("apiKeys.disableFailed"));
    } finally {
      setIsBusy(false);
    }
  }

  async function activateClient(client: ApiClient) {
    setIsBusy(true);
    setMessage(null);
    try {
      await api.apiClients.update(client.id, {
        tenantId,
        storeId: client.storeId,
        name: client.name,
        isActive: true,
        permissions: client.permissions,
      });
      setMessage(t("apiKeys.activated"));
      setClientToActivate(null);
      await loadClients(page);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("apiKeys.activateFailed"));
    } finally {
      setIsBusy(false);
    }
  }

  async function deleteClient(client: ApiClient) {
    setIsBusy(true);
    setMessage(null);
    try {
      await api.apiClients.delete(client.id, tenantId);
      setMessage(t("apiKeys.deleted"));
      setClientToDelete(null);
      await loadClients(page);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("apiKeys.deleteFailed"));
    } finally {
      setIsBusy(false);
    }
  }

  function togglePermission(permission: string, checked: boolean) {
    const perms = checked ? [...draft.permissions, permission] : draft.permissions.filter((p) => p !== permission);
    setDraft({ ...draft, permissions: [...new Set(perms)] });
  }

  function openCreateModal() {
    setSelectedClient(null);
    setDraft({
      ...initialDraft,
      storeId: currentStore?.id ?? "",
    });
    setCreatedKey(null);
    setCreatedKeyId(null);
    setErrors({});
    setMessage(null);
    setIsModalOpen(true);
  }

  function openEditModal(client: ApiClient) {
    setSelectedClient(client);
    setDraft({
      name: client.name,
      storeId: client.storeId ?? "",
      isActive: client.isActive,
      permissions: [...client.permissions],
    });
    setCreatedKey(null);
    setCreatedKeyId(null);
    setErrors({});
    setMessage(null);
    setIsModalOpen(true);
  }

  return (
    <section className="api-keys-panel">
      <div className="panel-section">
        <PageHeader
          title={t("apiKeys.title")}
          description={t("apiKeys.description")}
          actions={<button className="primary" type="button" onClick={openCreateModal}><KeyRound size={16} />{t("common.addNew")}</button>}
        />
        <SearchInput value={search} onChange={setSearch} onSearch={() => loadClients(1)} isLoading={isBusy} />
        <div className="context-hint">
          {currentTenant ? t("apiKeys.tenantContext").replace("{name}", currentTenant.name) : t("apiKeys.selectTenantLoad")}
          {currentStore ? ` · ${t("apiKeys.storeContext").replace("{name}", currentStore.name)}` : ` · ${t("apiKeys.tenantWide")}`}
        </div>

        {createdKey && (
          <div className="api-key-once">
            <div className="context-hint">
              {t("apiKeys.generatedCredentialWarning")}
            </div>
            <div className="row-actions" style={{ justifyContent: "flex-start", marginTop: "0.75rem" }}>
              <button className="secondary" type="button" onClick={() => setCredentialPreview({ title: t("apiKeys.generatedCredential"), value: createdKey, warning: t("apiKeys.generatedCredentialWarning") })}>
                <Eye size={16} />
                {t("apiKeys.viewGeneratedToken")}
              </button>
              {createdKeyId && (
                <button className="secondary" type="button" onClick={() => setCredentialPreview({ title: t("apiKeys.apiKeyId"), value: createdKeyId })}>
                  <Eye size={16} />
                  {t("apiKeys.viewId")}
                </button>
              )}
            </div>
          </div>
        )}

        {message && <div className={/created|updated|disabled|activated|deleted/i.test(message) ? "notice success" : "notice error"}>{message}</div>}
      </div>

      <div className="table-shell">
        <table>
          <thead>
            <tr>
              <th>{t("common.name")}</th>
              <th>{t("apiKeys.permissions")}</th>
              <th>{t("common.status")}</th>
              <th>{t("apiKeys.lastUsed")}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {clients.length === 0 ? (
              <tr><td colSpan={5}>{t("apiKeys.noClientsLoaded")}</td></tr>
            ) : (
              clients.map((client) => (
                <tr key={client.id}>
                  <td>{client.name}</td>
                  <td>{client.permissions.join(", ")}</td>
                  <td>{client.isActive ? t("common.active") : t("common.disabled")}</td>
                  <td>{client.lastUsedAt ? new Date(client.lastUsedAt).toLocaleString() : t("common.never")}</td>
                  <td>
                    <div className="row-actions">
                      <button className="secondary" onClick={() => openEditModal(client)} disabled={isBusy}><Edit3 size={16} />{t("common.edit")}</button>
                      <button className="secondary" onClick={() => setCredentialPreview({ title: t("apiKeys.apiKeyId"), value: client.id })} disabled={isBusy}><Eye size={16} />{t("apiKeys.viewId")}</button>
                      {client.isActive ? (
                        <button className="secondary" onClick={() => setClientToDisable(client)} disabled={isBusy}><PowerOff size={16} />{t("common.disable")}</button>
                      ) : (
                        <button className="secondary" onClick={() => setClientToActivate(client)} disabled={isBusy}><CheckCircle2 size={16} />{t("common.activate")}</button>
                      )}
                      <button className="secondary danger" onClick={() => setClientToDelete(client)} disabled={isBusy}><Trash2 size={16} />{t("common.delete")}</button>
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
        <PaginationControls pagination={pagination} onPageChange={loadClients} />
      </div>

      {isModalOpen && (
        <EntityModal
          title={selectedClient ? t("apiKeys.editApiKey") : t("apiKeys.newApiKey")}
          onClose={() => { setIsModalOpen(false); setSelectedClient(null); }}
          footer={(
            <>
              <button className="secondary" type="button" onClick={() => { setIsModalOpen(false); setSelectedClient(null); }}>{t("common.cancel")}</button>
              <button className="primary" type="button" onClick={saveClient} disabled={isBusy}>
                {selectedClient ? t("apiKeys.saveApiKey") : t("apiKeys.createApiKey")}
              </button>
            </>
          )}
        >
          <div className="form-grid">
            <Field label={t("common.name")} value={draft.name} error={errors["api.name"]} required onChange={(v) => setDraft({ ...draft, name: v })} />
            <label className="field">
              <span>{t("apiKeys.store")}</span>
              <select value={draft.storeId} onChange={(event) => setDraft({ ...draft, storeId: event.target.value })}>
                <option value="">{t("apiKeys.tenantWide")}</option>
                {(stores.length > 0
                  ? stores.filter((store) => store.isActive)
                  : availableStores.map((store) => ({ ...store, isActive: true }))).map((store) => (
                  <option key={store.id} value={store.id}>{store.name}</option>
                ))}
              </select>
            </label>
            <div className="context-pill">{currentTenant ? t("apiKeys.tenantContext").replace("{name}", currentTenant.name) : t("apiKeys.selectTenantLoad")}</div>
            {selectedClient && (
              <Toggle
                label={draft.isActive ? t("common.active") : t("common.inactive")}
                checked={draft.isActive}
                onChange={(checked) => setDraft({ ...draft, isActive: checked })}
              />
            )}
          </div>
          {selectedClient && (
            <div className="notice">
              {t("apiKeys.secretWarning")}
            </div>
          )}
          <div className="permission-row">
            {externalApiPermissions.map((perm) => (
              <Toggle key={perm} label={perm} checked={draft.permissions.includes(perm)} onChange={(c) => togglePermission(perm, c)} />
            ))}
          </div>
          {errors["api.permissions"] && <div className="field-error">{errors["api.permissions"]}</div>}
        </EntityModal>
      )}

      {clientToDisable && (
        <ConfirmDialog
          title={t("apiKeys.disableTitle")}
          message={t("apiKeys.disableMessage").replace("{name}", clientToDisable.name)}
          confirmLabel={t("common.disable")}
          onCancel={() => setClientToDisable(null)}
          onConfirm={() => disableClient(clientToDisable.id)}
        />
      )}

      {clientToActivate && (
        <ConfirmDialog
          title={t("apiKeys.activateTitle")}
          message={t("apiKeys.activateMessage").replace("{name}", clientToActivate.name)}
          confirmLabel={t("common.activate")}
          onCancel={() => setClientToActivate(null)}
          onConfirm={() => activateClient(clientToActivate)}
        />
      )}

      {clientToDelete && (
        <ConfirmDialog
          title={t("apiKeys.deleteTitle")}
          message={t("apiKeys.deleteMessage").replace("{name}", clientToDelete.name)}
          confirmLabel={t("common.delete")}
          onCancel={() => setClientToDelete(null)}
          onConfirm={() => deleteClient(clientToDelete)}
        />
      )}

      {credentialPreview && (
        <EntityModal
          title={credentialPreview.title}
          onClose={() => setCredentialPreview(null)}
          footer={(
            <>
              <button className="secondary" type="button" onClick={() => void navigator.clipboard?.writeText(credentialPreview.value)}>
                <Copy size={16} />
                {t("apiKeys.copy")}
              </button>
              <button className="primary" type="button" onClick={() => setCredentialPreview(null)}>{t("common.close")}</button>
            </>
          )}
        >
          {credentialPreview.warning && <div className="notice">{credentialPreview.warning}</div>}
          <label className="field api-key-once">
            <span>{credentialPreview.title}</span>
            <input value={credentialPreview.value} readOnly />
          </label>
        </EntityModal>
      )}
    </section>
  );
}

function validateClientDraft(draft: { name: string; permissions: string[] }) {
  const errors: Record<string, string> = {};
  addRequired(errors, "api.name", draft.name, "Name is required.");
  if (draft.permissions.length === 0) errors["api.permissions"] = "Choose at least one permission.";
  return errors;
}
