import { useEffect, useState } from "react";
import { CheckCircle2, Edit3, Plus, PowerOff } from "lucide-react";
import { ConfirmDialog, DetailGrid, EntityModal, PageHeader, SearchInput } from "../components/Crud";
import { Field } from "../components/Field";
import { PaginationControls } from "../components/PaginationControls";
import { api } from "../api";
import { useAppContext } from "../context/AppContext";
import { useI18n } from "../i18n";
import type { Pagination, StoreSummary } from "../types";
import { normalizeStoreCode, addRequired, addEmail, addPattern } from "../utils/validation";

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
  const { currentTenant } = useAppContext();
  const { t } = useI18n();
  const tenantId = currentTenant?.id ?? "";
  const [stores, setStores] = useState<StoreSummary[]>([]);
  const [selectedStoreId, setSelectedStoreId] = useState<string | null>(null);
  const [draft, setDraft] = useState(initialStore);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [pagination, setPagination] = useState<Pagination | null>(null);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [message, setMessage] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [storeToDisable, setStoreToDisable] = useState<StoreSummary | null>(null);
  const [storeToActivate, setStoreToActivate] = useState<StoreSummary | null>(null);
  const [detailStore, setDetailStore] = useState<StoreSummary | null>(null);

  useEffect(() => {
    if (!tenantId) {
      setStores([]);
      setPagination(null);
      return;
    }

    void loadStores(1);
  }, [tenantId]);

  async function loadStores(nextPage = page) {
    setMessage(null);
    if (!tenantId) {
      setMessage(t("stores.selectTenantLoad"));
      return;
    }

    setIsLoading(true);
    try {
      const data = await api.tenants.stores.list(tenantId.trim(), { page: nextPage, search });
      setStores(data.stores ?? []);
      setPagination(data.pagination ?? null);
      setPage(data.pagination?.page ?? nextPage);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("stores.loadFailed"));
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
    setDetailStore(null);
    setIsModalOpen(true);
  }

  function newStore() {
    setSelectedStoreId(null);
    setDraft({ ...initialStore, code: `STORE${stores.length + 1}` });
    setMessage(null);
    setErrors({});
    setIsModalOpen(true);
  }

  async function saveStore() {
    if (!tenantId) {
      setMessage(t("stores.selectTenantSave"));
      return;
    }

    const validationErrors = validateStoreDraft(draft);
    setErrors(validationErrors);
    setMessage(null);
    if (Object.keys(validationErrors).length > 0) return;

    try {
      if (selectedStoreId) {
        await api.tenants.stores.update(tenantId.trim(), selectedStoreId, draft);
        setMessage(t("stores.updated"));
      } else {
        await api.tenants.stores.create(tenantId.trim(), draft);
        setMessage(t("stores.created"));
      }
      await loadStores(selectedStoreId ? page : 1);
      setIsModalOpen(false);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("stores.saveFailed"));
    }
  }

  async function disableStore(storeId: string) {
    setMessage(null);
    try {
      await api.tenants.stores.disable(tenantId.trim(), storeId);
      setMessage(t("stores.disabled"));
      setStoreToDisable(null);
      await loadStores(page);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("stores.disableFailed"));
    }
  }

  async function activateStore(storeId: string) {
    setMessage(null);
    try {
      await api.tenants.stores.activate(tenantId.trim(), storeId);
      setMessage(t("stores.activated"));
      setStoreToActivate(null);
      await loadStores(page);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("stores.activateFailed"));
    }
  }

  return (
    <section className="stores-panel">
      <div className="panel-section">
        <PageHeader
          title={t("stores.title")}
          description={t("stores.description")}
          actions={<button className="primary" onClick={newStore}><Plus size={16} />{t("common.addNew")}</button>}
        />
        <SearchInput value={search} onChange={setSearch} onSearch={() => loadStores(1)} isLoading={isLoading} />
        {message && <div className={/created|updated|disabled|activated/i.test(message) ? "notice success" : "notice error"}>{message}</div>}
      </div>

      <div className="table-shell">
        <table>
          <thead>
            <tr>
              <th>{t("common.name")}</th>
              <th>{t("stores.code")}</th>
              <th>{t("stores.type")}</th>
              <th>{t("stores.contact")}</th>
              <th>{t("common.status")}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {stores.length === 0 ? (
              <tr><td colSpan={6}>{t("stores.noStoresLoaded")}</td></tr>
            ) : (
              stores.map((store) => (
                <tr key={store.id}>
                  <td><button className="row-link" onClick={() => setDetailStore(store)}>{store.name}</button></td>
                  <td>{store.code}</td>
                  <td>{store.type}</td>
                  <td>{store.email || store.phone || t("common.notSet")}</td>
                  <td>{store.isActive ? t("common.active") : t("common.disabled")}</td>
                  <td>
                    <div className="row-actions">
                      <button className="secondary" onClick={() => editStore(store)}><Edit3 size={16} />{t("common.edit")}</button>
                      {store.isActive ? (
                        <button className="secondary" onClick={() => setStoreToDisable(store)}><PowerOff size={16} />{t("common.disable")}</button>
                      ) : (
                        <button className="secondary" onClick={() => setStoreToActivate(store)}><CheckCircle2 size={16} />{t("common.activate")}</button>
                      )}
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
        <PaginationControls pagination={pagination} onPageChange={loadStores} />
      </div>

      {isModalOpen && (
        <EntityModal
          title={selectedStoreId ? t("stores.editStore") : t("stores.newStore")}
          onClose={() => setIsModalOpen(false)}
          footer={(
            <>
              <button className="secondary" type="button" onClick={() => setIsModalOpen(false)}>{t("common.cancel")}</button>
              <button className="primary" type="button" onClick={saveStore}>{selectedStoreId ? t("stores.updateStore") : t("stores.createStore")}</button>
            </>
          )}
        >
          <div className="form-grid">
            <Field label={t("stores.storeName")} value={draft.name} error={errors["store.name"]} required onChange={(v) => setDraft({ ...draft, name: v })} />
            <Field label={t("stores.code")} value={draft.code} error={errors["store.code"]} required onChange={(v) => setDraft({ ...draft, code: normalizeStoreCode(v) })} />
            <Field label={t("stores.type")} value={draft.type} error={errors["store.type"]} required onChange={(v) => setDraft({ ...draft, type: v })} />
            <Field label={t("stores.email")} type="email" value={draft.email} error={errors["store.email"]} onChange={(v) => setDraft({ ...draft, email: v })} />
            <Field label={t("stores.phone")} value={draft.phone} onChange={(v) => setDraft({ ...draft, phone: v })} />
            <Field label={t("stores.document")} value={draft.document} onChange={(v) => setDraft({ ...draft, document: v })} />
            <Field label={t("stores.address")} value={draft.addressLine} onChange={(v) => setDraft({ ...draft, addressLine: v })} />
            <Field label={t("stores.city")} value={draft.city} onChange={(v) => setDraft({ ...draft, city: v })} />
            <Field label={t("stores.state")} value={draft.state} onChange={(v) => setDraft({ ...draft, state: v })} />
            <Field label={t("stores.zipCode")} value={draft.zipCode} onChange={(v) => setDraft({ ...draft, zipCode: v })} />
          </div>
        </EntityModal>
      )}

      {storeToDisable && (
        <ConfirmDialog
          title={t("stores.disableTitle")}
          message={t("stores.disableMessage").replace("{name}", storeToDisable.name)}
          confirmLabel={t("common.disable")}
          onCancel={() => setStoreToDisable(null)}
          onConfirm={() => disableStore(storeToDisable.id)}
        />
      )}

      {storeToActivate && (
        <ConfirmDialog
          title={t("stores.activateTitle")}
          message={t("stores.activateMessage").replace("{name}", storeToActivate.name)}
          confirmLabel={t("common.activate")}
          onCancel={() => setStoreToActivate(null)}
          onConfirm={() => activateStore(storeToActivate.id)}
        />
      )}

      {detailStore && (
        <EntityModal
          title={t("stores.details")}
          onClose={() => setDetailStore(null)}
          footer={(
            <>
              <button className="secondary" type="button" onClick={() => setDetailStore(null)}>{t("common.close")}</button>
              <button className="primary" type="button" onClick={() => editStore(detailStore)}>{t("common.edit")}</button>
            </>
          )}
        >
          <DetailGrid
            items={[
              { label: t("common.name"), value: detailStore.name },
              { label: t("stores.code"), value: detailStore.code },
              { label: t("stores.type"), value: detailStore.type },
              { label: t("stores.contact"), value: detailStore.email || detailStore.phone || t("common.notSet") },
              { label: t("stores.address"), value: [detailStore.addressLine, detailStore.city, detailStore.state, detailStore.zipCode].filter(Boolean).join(", ") || t("common.notSet") },
              { label: t("common.status"), value: detailStore.isActive ? t("common.active") : t("common.disabled") },
            ]}
          />
        </EntityModal>
      )}
    </section>
  );
}

function validateStoreDraft(draft: typeof initialStore) {
  const errors: Record<string, string> = {};
  addRequired(errors, "store.name", draft.name, "Store name is required.");
  addRequired(errors, "store.code", draft.code, "Store code is required.");
  addPattern(errors, "store.code", draft.code, /^[A-Z0-9]+(?:-[A-Z0-9]+)*$/, "Use uppercase letters, numbers and hyphens.");
  addRequired(errors, "store.type", draft.type, "Store type is required.");
  addEmail(errors, "store.email", draft.email);
  return errors;
}
