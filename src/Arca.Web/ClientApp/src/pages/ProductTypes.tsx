import { useEffect, useState } from "react";
import { CheckCircle2, Edit3, Plus, PowerOff, Save, Trash2, X } from "lucide-react";
import { ConfirmDialog, DetailGrid, EntityModal, PageHeader, SearchInput } from "../components/Crud";
import { Field } from "../components/Field";
import { PaginationControls } from "../components/PaginationControls";
import { api } from "../api";
import { useAppContext } from "../context/AppContext";
import { useI18n } from "../i18n";
import type { ProductType, Pagination } from "../types";

const initialProductType = {
  name: "",
  description: "",
};

export function ProductTypes() {
  const { currentTenant } = useAppContext();
  const { t } = useI18n();
  const tenantId = currentTenant?.id ?? "";
  const [types, setTypes] = useState<ProductType[]>([]);
  const [message, setMessage] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [pagination, setPagination] = useState<Pagination | null>(null);
  const [selectedType, setSelectedType] = useState<ProductType | null>(null);
  const [typeToDisable, setTypeToDisable] = useState<ProductType | null>(null);
  const [typeToDelete, setTypeToDelete] = useState<ProductType | null>(null);
  const [draft, setDraft] = useState(initialProductType);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [detailType, setDetailType] = useState<ProductType | null>(null);

  useEffect(() => {
    if (!tenantId) {
      setTypes([]);
      setPagination(null);
      return;
    }

    void loadTypes(1);
  }, [tenantId]);

  async function loadTypes(nextPage = page) {
    if (!tenantId.trim()) {
      setMessage(t("productTypes.selectTenantLoad"));
      return;
    }
    setIsLoading(true);
    setMessage(null);
    try {
      const data = await api.catalog.productTypes.list(tenantId.trim(), {
        page: nextPage,
        pageSize: 25,
        search,
      });
      setTypes(data.productTypes);
      setPagination(data.pagination ?? null);
      setPage(data.pagination?.page ?? nextPage);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("productTypes.loadFailed"));
    } finally {
      setIsLoading(false);
    }
  }

  function newType() {
    setSelectedType(null);
    setDraft(initialProductType);
    setErrors({});
    setMessage(null);
    setDetailType(null);
    setIsModalOpen(true);
  }

  function editType(type: ProductType) {
    setSelectedType(type);
    setDraft({
      name: type.name,
      description: type.description ?? "",
    });
    setErrors({});
    setMessage(null);
    setIsModalOpen(true);
  }

  async function saveType() {
    if (!tenantId) {
      setMessage(t("productTypes.selectTenantSave"));
      return;
    }
    const validationErrors = validateProductTypeDraft(draft, t);
    setErrors(validationErrors);
    setMessage(null);
    if (Object.keys(validationErrors).length > 0) return;

    const payload = {
      tenantId: tenantId.trim(),
      name: draft.name.trim(),
      description: draft.description.trim() || null,
    };

    try {
      if (selectedType) {
        await api.catalog.productTypes.update(selectedType.id, payload);
        setMessage(t("productTypes.updated"));
      } else {
        await api.catalog.productTypes.create(payload);
        setMessage(t("productTypes.created"));
      }
      setIsModalOpen(false);
      await loadTypes(selectedType ? page : 1);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("productTypes.saveFailed"));
    }
  }

  async function disableType(id: string) {
    try {
      await api.catalog.productTypes.disable(id, tenantId.trim());
      setMessage(t("productTypes.disabled"));
      setTypeToDisable(null);
      await loadTypes();
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("productTypes.disableFailed"));
    }
  }

  async function activateType(id: string) {
    try {
      await api.catalog.productTypes.activate(id, tenantId.trim());
      setMessage(t("productTypes.activated"));
      await loadTypes();
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("productTypes.activateFailed"));
    }
  }

  async function deleteType(id: string) {
    try {
      await api.catalog.productTypes.delete(id, tenantId.trim());
      setMessage(t("productTypes.deleted"));
      setTypeToDelete(null);
      await loadTypes(page);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("productTypes.deleteFailed"));
    }
  }

  return (
    <section className="stores-panel">
      <div className="panel-section">
        <PageHeader
          title={t("productTypes.title")}
          description={t("productTypes.description")}
          actions={<button className="primary" onClick={newType}><Plus size={16} />{t("common.addNew")}</button>}
        />
        <SearchInput value={search} onChange={setSearch} onSearch={() => loadTypes(1)} isLoading={isLoading} />
        {message && <div className={isNoticeSuccess(message) ? "notice success" : "notice error"}>{message}</div>}
      </div>

      <div className="table-shell">
        <table>
          <thead>
            <tr>
              <th>{t("common.name")}</th>
              <th>{t("common.description")}</th>
              <th>{t("common.status")}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {types.length === 0 ? (
              <tr>
                <td colSpan={4}>{t("productTypes.noProductTypesLoaded")}</td>
              </tr>
            ) : (
              types.map((pt) => (
                <tr key={pt.id}>
                  <td><button className="row-link" onClick={() => setDetailType(pt)}>{pt.name}</button></td>
                  <td>{pt.description || "-"}</td>
                  <td>{pt.isActive ? t("common.active") : t("common.disabled")}</td>
                  <td>
                    <div className="row-actions">
                      <button className="secondary" onClick={() => editType(pt)}><Edit3 size={16} />{t("common.edit")}</button>
                      {pt.isActive ? (
                        <button className="secondary" onClick={() => setTypeToDisable(pt)}>
                          <PowerOff size={16} />
                          {t("common.disable")}
                        </button>
                      ) : (
                        <button className="secondary" onClick={() => activateType(pt.id)}>
                          <CheckCircle2 size={16} />
                          {t("common.activate")}
                        </button>
                      )}
                      <button className="secondary danger" onClick={() => setTypeToDelete(pt)}><Trash2 size={16} />{t("common.delete")}</button>
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
        <PaginationControls pagination={pagination} onPageChange={(nextPage) => loadTypes(nextPage)} />
      </div>

      {isModalOpen && (
        <EntityModal
          title={selectedType ? t("productTypes.editProductType") : t("productTypes.newProductType")}
          onClose={() => setIsModalOpen(false)}
          footer={(
            <>
              <button className="secondary" type="button" onClick={() => setIsModalOpen(false)}><X size={16} />{t("common.cancel")}</button>
              <button className="primary" type="button" onClick={saveType}><Save size={16} />{selectedType ? t("productTypes.updateProductType") : t("productTypes.createProductType")}</button>
            </>
          )}
        >
          <div className="form-grid">
            <Field label={t("common.name")} value={draft.name} error={errors.name} required onChange={(value) => setDraft({ ...draft, name: value })} />
            <Field label={t("common.description")} value={draft.description} onChange={(value) => setDraft({ ...draft, description: value })} />
          </div>
        </EntityModal>
      )}

      {typeToDisable && (
        <ConfirmDialog
          title={t("productTypes.disableTitle")}
          message={t("productTypes.disableMessage").replace("{{name}}", typeToDisable.name)}
          confirmLabel={t("common.disable")}
          onCancel={() => setTypeToDisable(null)}
          onConfirm={() => disableType(typeToDisable.id)}
        />
      )}

      {typeToDelete && (
        <ConfirmDialog
          title={t("productTypes.deleteTitle")}
          message={t("productTypes.deleteMessage").replace("{{name}}", typeToDelete.name)}
          confirmLabel={t("common.delete")}
          onCancel={() => setTypeToDelete(null)}
          onConfirm={() => deleteType(typeToDelete.id)}
        />
      )}

      {detailType && (
        <EntityModal
          title={t("productTypes.details")}
          onClose={() => setDetailType(null)}
          footer={(
            <>
              <button className="secondary" type="button" onClick={() => setDetailType(null)}><X size={16} />{t("common.close")}</button>
              <button className="primary" type="button" onClick={() => editType(detailType)}><Edit3 size={16} />{t("common.edit")}</button>
            </>
          )}
        >
          <DetailGrid
            items={[
              { label: t("common.name"), value: detailType.name },
              { label: t("common.description"), value: detailType.description ?? "-" },
              { label: t("common.status"), value: detailType.isActive ? t("common.active") : t("common.disabled") },
              { label: t("categories.createdAt"), value: new Date(detailType.createdAt).toLocaleString() },
            ]}
          />
        </EntityModal>
      )}
    </section>
  );
}

function validateProductTypeDraft(draft: typeof initialProductType, t: (key: string) => string) {
  const errors: Record<string, string> = {};
  if (!draft.name.trim()) errors.name = t("productTypes.nameRequired");
  return errors;
}

function isNoticeSuccess(message: string) {
  return !/(failed|required|select|falha|obrigat|selecione|erro|invalid|inválid)/i.test(message);
}
