import { useEffect, useState } from "react";
import { CheckCircle2, Edit3, FolderPlus, Plus, PlusCircle, PowerOff, Save, Trash2, X } from "lucide-react";
import { ConfirmDialog, DetailGrid, EntityModal, PageHeader, SearchInput } from "../components/Crud";
import { Field } from "../components/Field";
import { PaginationControls } from "../components/PaginationControls";
import { api } from "../api";
import { useAppContext } from "../context/AppContext";
import { useI18n } from "../i18n";
import type { Category, Pagination } from "../types";
import { buildCategoryOptions, getCategoryDepth, getCategoryPathLabel } from "../utils/categories";

const initialCategory = {
  parentCategoryId: "",
  name: "",
  description: "",
  sortOrder: "0",
};

export function Categories() {
  const { currentTenant } = useAppContext();
  const { t } = useI18n();
  const tenantId = currentTenant?.id ?? "";
  const [categories, setCategories] = useState<Category[]>([]);
  const [message, setMessage] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [pagination, setPagination] = useState<Pagination | null>(null);
  const [selectedCategory, setSelectedCategory] = useState<Category | null>(null);
  const [categoryToDisable, setCategoryToDisable] = useState<Category | null>(null);
  const [categoryToDelete, setCategoryToDelete] = useState<Category | null>(null);
  const [draft, setDraft] = useState(initialCategory);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [detailCategory, setDetailCategory] = useState<Category | null>(null);

  useEffect(() => {
    if (!tenantId) {
      setCategories([]);
      setPagination(null);
      return;
    }

    void loadCategories(1);
  }, [tenantId]);

  async function loadCategories(nextPage = page) {
    if (!tenantId.trim()) {
      setMessage(t("categories.selectTenant"));
      return;
    }
    setIsLoading(true);
    setMessage(null);
    try {
      const data = await api.catalog.categories.list(tenantId.trim(), {
        page: nextPage,
        pageSize: 25,
        search,
      });
      setCategories(data.categories);
      setPagination(data.pagination ?? null);
      setPage(data.pagination?.page ?? nextPage);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("categories.loadFailed"));
    } finally {
      setIsLoading(false);
    }
  }

  async function disableCategory(id: string) {
    try {
      await api.catalog.categories.disable(id, tenantId.trim());
      setMessage(t("categories.disabled"));
      await loadCategories();
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("categories.disableFailed"));
    }
  }

  async function activateCategory(id: string) {
    try {
      await api.catalog.categories.activate(id, tenantId.trim());
      setMessage(t("categories.activated"));
      await loadCategories();
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("categories.activateFailed"));
    }
  }

  async function deleteCategory(id: string) {
    try {
      await api.catalog.categories.delete(id, tenantId.trim());
      setMessage(t("categories.deleted"));
      await loadCategories(page);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("categories.deleteFailed"));
    }
  }

  function newCategory() {
    setSelectedCategory(null);
    setDraft(initialCategory);
    setErrors({});
    setMessage(null);
    setDetailCategory(null);
    setIsModalOpen(true);
  }

  function newSubcategory(parent: Category) {
    setSelectedCategory(null);
    setDraft({
      ...initialCategory,
      parentCategoryId: parent.id,
      sortOrder: String(parent.sortOrder + 1),
    });
    setErrors({});
    setMessage(null);
    setDetailCategory(null);
    setIsModalOpen(true);
  }

  function editCategory(category: Category) {
    setSelectedCategory(category);
    setDraft({
      parentCategoryId: category.parentCategoryId ?? "",
      name: category.name,
      description: category.description ?? "",
      sortOrder: String(category.sortOrder),
    });
    setErrors({});
    setMessage(null);
    setIsModalOpen(true);
  }

  async function saveCategory() {
    if (!tenantId.trim()) {
      setMessage(t("categories.selectTenantSave"));
      return;
    }

    const validationErrors = validateCategoryDraft(draft);
    setErrors(validationErrors);
    setMessage(null);
    if (Object.keys(validationErrors).length > 0) return;

    const payload = {
      tenantId: tenantId.trim(),
      parentCategoryId: draft.parentCategoryId || null,
      name: draft.name.trim(),
      description: draft.description.trim() || null,
      sortOrder: Number(draft.sortOrder),
    };

    try {
      if (selectedCategory) {
        await api.catalog.categories.update(selectedCategory.id, payload);
        setMessage(t("categories.updated"));
      } else {
        await api.catalog.categories.create(payload);
        setMessage(t("categories.created"));
      }
      setIsModalOpen(false);
      await loadCategories(selectedCategory ? page : 1);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("categories.saveFailed"));
    }
  }

  return (
    <section className="stores-panel">
      <div className="panel-section">
        <PageHeader
          title={t("categories.title")}
          description={t("categories.description")}
          actions={<button className="primary" onClick={newCategory}><Plus size={16} />{t("categories.addNew")}</button>}
        />
        <SearchInput value={search} onChange={setSearch} onSearch={() => loadCategories(1)} isLoading={isLoading} />
        {message && <div className={/created|updated|disabled|activated|deleted|criada|atualizada|desativada|ativada|excluída/i.test(message) ? "notice success" : "notice error"}>{message}</div>}
      </div>

      <div className="table-shell">
        <table>
          <thead>
            <tr>
              <th>{t("categories.name")}</th>
              <th>{t("categories.slug")}</th>
              <th>{t("categories.order")}</th>
              <th>{t("common.status")}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {categories.length === 0 ? (
              <tr>
                <td colSpan={5}>{t("categories.noCategoriesLoaded")}</td>
              </tr>
            ) : (
              [...categories]
                .sort((left, right) => getCategoryPathLabel(categories, left.id).localeCompare(getCategoryPathLabel(categories, right.id)))
                .map((cat) => (
                <tr key={cat.id}>
                  <td>
                    <button
                      className="row-link"
                      style={{ paddingLeft: `${getCategoryDepth(categories, cat) * 18}px` }}
                      onClick={() => setDetailCategory(cat)}
                    >
                      {getCategoryPathLabel(categories, cat.id)}
                    </button>
                  </td>
                  <td>{cat.slug}</td>
                  <td>{cat.sortOrder}</td>
                  <td>{cat.isActive ? t("common.active") : t("common.inactive")}</td>
                  <td>
                    <div className="row-actions">
                      {cat.isActive && (
                        <button className="secondary" onClick={() => newSubcategory(cat)}><FolderPlus size={16} />{t("categories.addSubcategory")}</button>
                      )}
                      <button className="secondary" onClick={() => editCategory(cat)}><Edit3 size={16} />{t("common.edit")}</button>
                      {cat.isActive ? (
                        <button className="secondary" onClick={() => setCategoryToDisable(cat)}>
                          <PowerOff size={16} />
                          {t("common.disable")}
                        </button>
                      ) : (
                        <button className="secondary" onClick={() => activateCategory(cat.id)}>
                          <CheckCircle2 size={16} />
                          {t("common.activate")}
                        </button>
                      )}
                      <button className="secondary danger" onClick={() => setCategoryToDelete(cat)}><Trash2 size={16} />{t("common.delete")}</button>
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
        <PaginationControls pagination={pagination} onPageChange={(nextPage) => loadCategories(nextPage)} />
      </div>

      {isModalOpen && (
        <EntityModal
          title={selectedCategory ? t("categories.editCategory") : draft.parentCategoryId ? t("categories.newSubcategory") : t("categories.newCategory")}
          onClose={() => setIsModalOpen(false)}
          footer={(
            <>
              <button className="secondary" type="button" onClick={() => setIsModalOpen(false)}><X size={16} />{t("common.cancel")}</button>
              <button className="primary" type="button" onClick={saveCategory}><Save size={16} />{selectedCategory ? t("categories.updateCategory") : t("categories.createCategory")}</button>
            </>
          )}
        >
          {draft.parentCategoryId && !selectedCategory && (
            <div className="context-pill">
              {t("categories.subcategoryOf")}: {getCategoryPathLabel(categories, draft.parentCategoryId)}
            </div>
          )}
          <div className="form-grid">
            <label className="field">
              <span>{t("categories.parentCategory")}</span>
              <select value={draft.parentCategoryId} onChange={(event) => setDraft({ ...draft, parentCategoryId: event.target.value })}>
                <option value="">{t("categories.mainCategory")}</option>
                {buildCategoryOptions(categories, selectedCategory?.id).map((option) => (
                  <option key={option.id} value={option.id}>{option.label}</option>
                ))}
              </select>
              <small className="context-hint">{t("categories.parentHelp")}</small>
            </label>
            <Field label={t("categories.name")} value={draft.name} error={errors.name} required onChange={(value) => setDraft({ ...draft, name: value })} />
            <Field label={t("categories.descriptionField")} value={draft.description} onChange={(value) => setDraft({ ...draft, description: value })} />
            <Field label={t("categories.sortOrder")} value={draft.sortOrder} error={errors.sortOrder} required onChange={(value) => setDraft({ ...draft, sortOrder: value.replace(/[^\d-]/g, "") })} />
          </div>
        </EntityModal>
      )}

      {categoryToDisable && (
        <ConfirmDialog
          title={t("categories.disableTitle")}
          message={t("categories.disableMessage").replace("{{name}}", categoryToDisable.name)}
          confirmLabel={t("common.disable")}
          onCancel={() => setCategoryToDisable(null)}
          onConfirm={() => {
            const id = categoryToDisable.id;
            setCategoryToDisable(null);
            void disableCategory(id);
          }}
        />
      )}

      {categoryToDelete && (
        <ConfirmDialog
          title={t("categories.deleteTitle")}
          message={t("categories.deleteMessage").replace("{{name}}", categoryToDelete.name)}
          confirmLabel={t("common.delete")}
          onCancel={() => setCategoryToDelete(null)}
          onConfirm={() => {
            const id = categoryToDelete.id;
            setCategoryToDelete(null);
            void deleteCategory(id);
          }}
        />
      )}

      {detailCategory && (
        <EntityModal
          title={t("categories.details")}
          onClose={() => setDetailCategory(null)}
          footer={(
            <>
              <button className="secondary" type="button" onClick={() => setDetailCategory(null)}><X size={16} />{t("common.close")}</button>
              {detailCategory.isActive && (
                <button className="secondary" type="button" onClick={() => newSubcategory(detailCategory)}><PlusCircle size={16} />{t("categories.addSubcategory")}</button>
              )}
              <button className="primary" type="button" onClick={() => editCategory(detailCategory)}><Edit3 size={16} />{t("common.edit")}</button>
            </>
          )}
        >
          <DetailGrid
            items={[
              { label: t("categories.name"), value: detailCategory.name },
              { label: t("categories.parent"), value: getCategoryPathLabel(categories, detailCategory.parentCategoryId) },
              { label: t("categories.slug"), value: detailCategory.slug },
              { label: t("categories.descriptionField"), value: detailCategory.description ?? "-" },
              { label: t("categories.sortOrder"), value: detailCategory.sortOrder },
              { label: t("common.status"), value: detailCategory.isActive ? t("common.active") : t("common.inactive") },
              { label: t("categories.createdAt"), value: new Date(detailCategory.createdAt).toLocaleString() },
            ]}
          />
        </EntityModal>
      )}
    </section>
  );
}

function validateCategoryDraft(draft: typeof initialCategory) {
  const errors: Record<string, string> = {};
  if (!draft.name.trim()) errors.name = "Name is required.";
  if (!draft.sortOrder.trim() || Number.isNaN(Number(draft.sortOrder))) {
    errors.sortOrder = "Sort order must be a number.";
  }
  return errors;
}
