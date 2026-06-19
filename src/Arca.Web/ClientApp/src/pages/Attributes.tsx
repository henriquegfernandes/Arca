import { useEffect, useState } from "react";
import { CheckCircle2, Edit3, ListPlus, Plus, PowerOff, Save, Trash2, X } from "lucide-react";
import { ConfirmDialog, DetailGrid, EntityModal, PageHeader, SearchInput } from "../components/Crud";
import { Field } from "../components/Field";
import { PaginationControls } from "../components/PaginationControls";
import { Toggle } from "../components/Toggle";
import { api } from "../api";
import { useAppContext } from "../context/AppContext";
import { useI18n } from "../i18n";
import type { ProductAttribute, AttributeValue, Pagination } from "../types";
import { normalizeAttributeCode } from "../utils/validation";

export function Attributes() {
  const { currentTenant } = useAppContext();
  const { t } = useI18n();
  const tenantId = currentTenant?.id ?? "";
  const [attributes, setAttributes] = useState<ProductAttribute[]>([]);
  const [message, setMessage] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [pagination, setPagination] = useState<Pagination | null>(null);

  const [name, setName] = useState("");
  const [code, setCode] = useState("");
  const [attrType, setAttrType] = useState("Select");
  const [isVariant, setIsVariant] = useState(true);
  const [isRequired, setIsRequired] = useState(false);
  const [sortOrder, setSortOrder] = useState("1");

  const [selectedAttrId, setSelectedAttrId] = useState<string | null>(null);
  const [values, setValues] = useState<AttributeValue[]>([]);
  const [valName, setValName] = useState("");
  const [valCode, setValCode] = useState("");
  const [valHex, setValHex] = useState("");
  const [editingAttribute, setEditingAttribute] = useState<ProductAttribute | null>(null);
  const [attributeToDisable, setAttributeToDisable] = useState<ProductAttribute | null>(null);
  const [attributeToDelete, setAttributeToDelete] = useState<ProductAttribute | null>(null);
  const [isAttributeModalOpen, setIsAttributeModalOpen] = useState(false);
  const [isValueModalOpen, setIsValueModalOpen] = useState(false);
  const [editingValue, setEditingValue] = useState<AttributeValue | null>(null);
  const [valueToDisable, setValueToDisable] = useState<AttributeValue | null>(null);
  const [valueToDelete, setValueToDelete] = useState<AttributeValue | null>(null);
  const [detailAttribute, setDetailAttribute] = useState<ProductAttribute | null>(null);
  const [detailValue, setDetailValue] = useState<AttributeValue | null>(null);

  useEffect(() => {
    if (!tenantId) {
      setAttributes([]);
      setPagination(null);
      setSelectedAttrId(null);
      setValues([]);
      return;
    }

    void loadAttributes(1);
  }, [tenantId]);

  async function loadAttributes(nextPage = page) {
    if (!tenantId.trim()) {
      setMessage(t("attributes.selectTenantLoad"));
      return;
    }
    setIsLoading(true);
    setMessage(null);
    try {
      const data = await api.catalog.attributes.list(tenantId.trim(), {
        page: nextPage,
        pageSize: 25,
        search,
      });
      setAttributes(data.attributes);
      setPagination(data.pagination ?? null);
      setPage(data.pagination?.page ?? nextPage);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("attributes.loadFailed"));
    } finally {
      setIsLoading(false);
    }
  }

  function newAttribute() {
    setEditingAttribute(null);
    setName("");
    setCode("");
    setAttrType("Select");
    setIsVariant(true);
    setIsRequired(false);
    setSortOrder("1");
    setMessage(null);
    setIsAttributeModalOpen(true);
  }

  function editAttribute(attribute: ProductAttribute) {
    setEditingAttribute(attribute);
    setName(attribute.name);
    setCode(attribute.code);
    setAttrType(attribute.attributeType);
    setIsVariant(attribute.isVariantAttribute);
    setIsRequired(attribute.isRequired);
    setSortOrder(String(attribute.sortOrder));
    setMessage(null);
    setDetailAttribute(null);
    setIsAttributeModalOpen(true);
  }

  async function saveAttribute() {
    if (!name.trim() || !code.trim()) {
      setMessage(t("attributes.nameCodeRequired"));
      return;
    }
    if (!tenantId) {
      setMessage(t("attributes.selectTenantSave"));
      return;
    }
    try {
      const payload = {
        tenantId: tenantId.trim(),
        name: name.trim(),
        code: normalizeAttributeCode(code),
        attributeType: attrType,
        isVariantAttribute: isVariant,
        isRequired,
        sortOrder: parseInt(sortOrder) || 1,
      };
      if (editingAttribute) {
        await api.catalog.attributes.update(editingAttribute.id, payload);
        setMessage(t("attributes.updated"));
      } else {
        await api.catalog.attributes.create(payload);
        setMessage(t("attributes.created"));
      }
      setName("");
      setCode("");
      setEditingAttribute(null);
      setIsAttributeModalOpen(false);
      await loadAttributes();
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("attributes.saveFailed"));
    }
  }

  async function disableAttribute(id: string) {
    try {
      await api.catalog.attributes.disable(id, tenantId.trim());
      setMessage(t("attributes.disabled"));
      if (selectedAttrId === id) {
        setSelectedAttrId(null);
        setValues([]);
      }
      setAttributeToDisable(null);
      await loadAttributes();
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("attributes.disableFailed"));
    }
  }

  async function activateAttribute(id: string) {
    try {
      await api.catalog.attributes.activate(id, tenantId.trim());
      setMessage(t("attributes.activated"));
      await loadAttributes();
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("attributes.activateFailed"));
    }
  }

  async function deleteAttribute(id: string) {
    try {
      await api.catalog.attributes.delete(id, tenantId.trim());
      setMessage(t("attributes.deleted"));
      if (selectedAttrId === id) {
        setSelectedAttrId(null);
        setValues([]);
      }
      setAttributeToDelete(null);
      await loadAttributes(page);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("attributes.deleteFailed"));
    }
  }

  async function loadValues(attrId: string) {
    setSelectedAttrId(attrId);
    setValName("");
    setValCode("");
    setValHex("");
    setEditingValue(null);
    try {
      const data = await api.catalog.attributes.values.list(attrId, tenantId.trim());
      setValues(data.values);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("attributes.valuesLoadFailed"));
    }
  }

  function newValue() {
    setEditingValue(null);
    setValName("");
    setValCode("");
    setValHex("");
    setMessage(null);
    setIsValueModalOpen(true);
  }

  function editValue(value: AttributeValue) {
    setEditingValue(value);
    setValName(value.name);
    setValCode(value.code);
    setValHex(value.hexCode ?? "");
    setMessage(null);
    setDetailValue(null);
    setIsValueModalOpen(true);
  }

  async function saveValue() {
    if (!selectedAttrId || !valName.trim() || !valCode.trim()) {
      setMessage(t("attributes.valueNameCodeRequired"));
      return;
    }
    try {
      const payload = {
        tenantId: tenantId.trim(),
        productAttributeId: selectedAttrId,
        name: valName.trim(),
        code: normalizeAttributeCode(valCode),
        hexCode: valHex.trim() || null,
        sortOrder: editingValue?.sortOrder ?? values.length + 1,
      };
      if (editingValue) {
        await api.catalog.attributes.values.update(selectedAttrId, editingValue.id, payload);
        setMessage(t("attributes.valueUpdated"));
      } else {
        await api.catalog.attributes.values.create(selectedAttrId, payload);
        setMessage(t("attributes.valueCreated"));
      }
      setValName("");
      setValCode("");
      setValHex("");
      setEditingValue(null);
      setIsValueModalOpen(false);
      await loadValues(selectedAttrId);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("attributes.valueSaveFailed"));
    }
  }

  async function disableValue(valueId: string) {
    if (!selectedAttrId) return;
    try {
      await api.catalog.attributes.values.disable(selectedAttrId, valueId, tenantId.trim());
      setValueToDisable(null);
      await loadValues(selectedAttrId);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("attributes.valueDisableFailed"));
    }
  }

  async function activateValue(valueId: string) {
    if (!selectedAttrId) return;
    try {
      await api.catalog.attributes.values.activate(selectedAttrId, valueId, tenantId.trim());
      setMessage(t("attributes.valueActivated"));
      await loadValues(selectedAttrId);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("attributes.valueActivateFailed"));
    }
  }

  async function deleteValue(valueId: string) {
    if (!selectedAttrId) return;
    try {
      await api.catalog.attributes.values.delete(selectedAttrId, valueId, tenantId.trim());
      setMessage(t("attributes.valueDeleted"));
      setValueToDelete(null);
      await loadValues(selectedAttrId);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("attributes.valueDeleteFailed"));
    }
  }

  const attributeTypes = ["Select", "MultiSelect", "Text", "Number", "Boolean", "Date", "Decimal"];

  return (
    <section className="stores-panel">
      <div className="panel-section">
        <PageHeader
          title={t("attributes.title")}
          description={t("attributes.description")}
          actions={<button className="primary" type="button" onClick={newAttribute}><Plus size={16} />{t("common.addNew")}</button>}
        />
        <SearchInput value={search} onChange={setSearch} onSearch={() => loadAttributes(1)} isLoading={isLoading} />
        {message && (
          <div className={isNoticeSuccess(message) ? "notice success" : "notice error"}>
            {message}
          </div>
        )}
      </div>

      <div className="table-shell">
        <table>
          <thead>
            <tr>
              <th>{t("common.name")}</th>
              <th>{t("attributes.code")}</th>
              <th>{t("attributes.type")}</th>
              <th>{t("attributes.variant")}</th>
              <th>{t("categories.order")}</th>
              <th>{t("common.status")}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {attributes.length === 0 ? (
              <tr><td colSpan={7}>{t("attributes.noAttributesLoaded")}</td></tr>
            ) : (
              attributes.map((attr) => (
                <tr key={attr.id}>
                  <td><button className="row-link" onClick={() => setDetailAttribute(attr)}>{attr.name}</button></td>
                  <td>{attr.code}</td>
                  <td>{attr.attributeType}</td>
                  <td>{attr.isVariantAttribute ? t("common.yes") : t("common.no")}</td>
                  <td>{attr.sortOrder}</td>
                  <td>{attr.isActive ? t("common.active") : t("common.disabled")}</td>
                  <td>
                    <div className="row-actions">
                      {usesPredefinedValues(attr.attributeType) && (
                        <button className="secondary" onClick={() => loadValues(attr.id)}>
                          <ListPlus size={16} />
                          {t("attributes.values")}
                        </button>
                      )}
                      <button className="secondary" onClick={() => editAttribute(attr)}><Edit3 size={16} />{t("common.edit")}</button>
                      {attr.isActive ? (
                        <button className="secondary" onClick={() => setAttributeToDisable(attr)}>
                          <PowerOff size={16} />
                          {t("common.disable")}
                        </button>
                      ) : (
                        <button className="secondary" onClick={() => activateAttribute(attr.id)}>
                          <CheckCircle2 size={16} />
                          {t("common.activate")}
                        </button>
                      )}
                      <button className="secondary danger" onClick={() => setAttributeToDelete(attr)}><Trash2 size={16} />{t("common.delete")}</button>
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
        <PaginationControls pagination={pagination} onPageChange={(nextPage) => loadAttributes(nextPage)} />
      </div>

      {selectedAttrId && (
        <div className="panel-section">
          <div className="section-heading">
            <div>
              <h2>{t("attributes.attributeValues")}</h2>
              <p>{t("attributes.attributeValuesDescription")}</p>
            </div>
            <button className="primary" type="button" onClick={newValue}><Plus size={16} />{t("attributes.addValue")}</button>
          </div>
          <div className="table-shell" style={{ marginTop: "1rem" }}>
            <table>
              <thead>
                <tr>
                  <th>{t("common.name")}</th>
                  <th>{t("attributes.code")}</th>
                  <th>{t("attributes.color")}</th>
                  <th>{t("categories.order")}</th>
                  <th>{t("common.status")}</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {values.length === 0 ? (
                  <tr><td colSpan={6}>{t("attributes.noValues")}</td></tr>
                ) : (
                  values.map((v) => (
                    <tr key={v.id}>
                      <td><button className="row-link" onClick={() => setDetailValue(v)}>{v.name}</button></td>
                      <td>{v.code}</td>
                      <td>{v.hexCode ? <ColorBadge hex={v.hexCode} /> : "-"}</td>
                      <td>{v.sortOrder}</td>
                      <td>{v.isActive ? t("common.active") : t("common.disabled")}</td>
                      <td>
                        <div className="row-actions">
                          <button className="secondary" onClick={() => editValue(v)}><Edit3 size={16} />{t("common.edit")}</button>
                          {v.isActive ? (
                            <button className="secondary" onClick={() => setValueToDisable(v)}><PowerOff size={16} />{t("common.disable")}</button>
                          ) : (
                            <button className="secondary" onClick={() => activateValue(v.id)}><CheckCircle2 size={16} />{t("common.activate")}</button>
                          )}
                          <button className="secondary danger" onClick={() => setValueToDelete(v)}><Trash2 size={16} />{t("common.delete")}</button>
                        </div>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {isAttributeModalOpen && (
        <EntityModal
          title={editingAttribute ? t("attributes.editAttribute") : t("attributes.newAttribute")}
          onClose={() => setIsAttributeModalOpen(false)}
          footer={(
            <>
              <button className="secondary" type="button" onClick={() => setIsAttributeModalOpen(false)}><X size={16} />{t("common.cancel")}</button>
              <button className="primary" type="button" onClick={saveAttribute}><Save size={16} />{editingAttribute ? t("attributes.updateAttribute") : t("attributes.createAttribute")}</button>
            </>
          )}
        >
          <div className="form-grid">
            <Field label={t("common.name")} value={name} required onChange={setName} />
            <Field label={t("attributes.code")} value={code} required onChange={(v) => setCode(normalizeAttributeCode(v))} />
            <label className="field">
              <span>{t("attributes.type")} *</span>
              <select value={attrType} onChange={(e) => setAttrType(e.target.value)}>
                {attributeTypes.map((t) => (
                  <option key={t} value={t}>{t}</option>
                ))}
              </select>
            </label>
            <Field label={t("categories.sortOrder")} value={sortOrder} onChange={setSortOrder} />
            <Toggle label={t("attributes.variantAttribute")} checked={isVariant} onChange={setIsVariant} />
            <Toggle label={t("attributes.required")} checked={isRequired} onChange={setIsRequired} />
          </div>
        </EntityModal>
      )}

      {isValueModalOpen && (
        <EntityModal
          title={editingValue ? t("attributes.editValue") : t("attributes.newValue")}
          onClose={() => setIsValueModalOpen(false)}
          footer={(
            <>
              <button className="secondary" type="button" onClick={() => setIsValueModalOpen(false)}><X size={16} />{t("common.cancel")}</button>
              <button className="primary" type="button" onClick={saveValue}><Save size={16} />{editingValue ? t("attributes.updateValue") : t("attributes.createValue")}</button>
            </>
          )}
        >
          <div className="form-grid">
            <Field label={t("attributes.valueName")} value={valName} required onChange={setValName} />
            <Field label={t("attributes.valueCode")} value={valCode} required onChange={(v) => setValCode(normalizeAttributeCode(v))} />
            <Field label={t("attributes.hexColor")} value={valHex} onChange={setValHex} placeholder="#000000" />
          </div>
        </EntityModal>
      )}

      {attributeToDisable && (
        <ConfirmDialog
          title={t("attributes.disableTitle")}
          message={t("attributes.disableMessage").replace("{{name}}", attributeToDisable.name)}
          confirmLabel={t("common.disable")}
          onCancel={() => setAttributeToDisable(null)}
          onConfirm={() => disableAttribute(attributeToDisable.id)}
        />
      )}

      {attributeToDelete && (
        <ConfirmDialog
          title={t("attributes.deleteTitle")}
          message={t("attributes.deleteMessage").replace("{{name}}", attributeToDelete.name)}
          confirmLabel={t("common.delete")}
          onCancel={() => setAttributeToDelete(null)}
          onConfirm={() => deleteAttribute(attributeToDelete.id)}
        />
      )}

      {valueToDisable && (
        <ConfirmDialog
          title={t("attributes.disableValueTitle")}
          message={t("attributes.disableValueMessage").replace("{{name}}", valueToDisable.name)}
          confirmLabel={t("common.disable")}
          onCancel={() => setValueToDisable(null)}
          onConfirm={() => disableValue(valueToDisable.id)}
        />
      )}

      {valueToDelete && (
        <ConfirmDialog
          title={t("attributes.deleteValueTitle")}
          message={t("attributes.deleteValueMessage").replace("{{name}}", valueToDelete.name)}
          confirmLabel={t("common.delete")}
          onCancel={() => setValueToDelete(null)}
          onConfirm={() => deleteValue(valueToDelete.id)}
        />
      )}

      {detailAttribute && (
        <EntityModal
          title={t("attributes.details")}
          onClose={() => setDetailAttribute(null)}
          footer={(
            <>
              <button className="secondary" type="button" onClick={() => setDetailAttribute(null)}><X size={16} />{t("common.close")}</button>
              <button className="primary" type="button" onClick={() => editAttribute(detailAttribute)}><Edit3 size={16} />{t("common.edit")}</button>
            </>
          )}
        >
          <DetailGrid
            items={[
              { label: t("common.name"), value: detailAttribute.name },
              { label: t("attributes.code"), value: detailAttribute.code },
              { label: t("attributes.type"), value: detailAttribute.attributeType },
              { label: t("attributes.variant"), value: detailAttribute.isVariantAttribute ? t("common.yes") : t("common.no") },
              { label: t("attributes.required"), value: detailAttribute.isRequired ? t("common.yes") : t("common.no") },
              { label: t("common.status"), value: detailAttribute.isActive ? t("common.active") : t("common.disabled") },
            ]}
          />
        </EntityModal>
      )}

      {detailValue && (
        <EntityModal
          title={t("attributes.valueDetails")}
          onClose={() => setDetailValue(null)}
          footer={(
            <>
              <button className="secondary" type="button" onClick={() => setDetailValue(null)}><X size={16} />{t("common.close")}</button>
              <button className="primary" type="button" onClick={() => editValue(detailValue)}><Edit3 size={16} />{t("common.edit")}</button>
            </>
          )}
        >
          <DetailGrid
            items={[
              { label: t("common.name"), value: detailValue.name },
              { label: t("attributes.code"), value: detailValue.code },
              { label: t("attributes.value"), value: detailValue.value ?? "-" },
              { label: t("attributes.color"), value: detailValue.hexCode ? <ColorBadge hex={detailValue.hexCode} /> : "-" },
              { label: t("categories.sortOrder"), value: detailValue.sortOrder },
              { label: t("common.status"), value: detailValue.isActive ? t("common.active") : t("common.disabled") },
            ]}
          />
        </EntityModal>
      )}
    </section>
  );
}

function isNoticeSuccess(message: string) {
  return !/(failed|required|select|falha|obrigat|selecione|erro|invalid|inválid)/i.test(message);
}

function usesPredefinedValues(attributeType: string) {
  return ["Select", "MultiSelect"].includes(attributeType);
}

function ColorBadge({ hex }: { hex: string }) {
  const color = textColorForBackground(hex);
  return <span className="color-badge" style={{ background: hex, color }}>{hex}</span>;
}

function textColorForBackground(hex: string) {
  const normalized = hex.replace("#", "");
  if (!/^[0-9a-fA-F]{6}$/.test(normalized)) return "#1f2933";
  const r = parseInt(normalized.slice(0, 2), 16);
  const g = parseInt(normalized.slice(2, 4), 16);
  const b = parseInt(normalized.slice(4, 6), 16);
  const luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255;
  return luminance > 0.58 ? "#1f2933" : "#ffffff";
}
