import { useState } from "react";
import { Field } from "../components/Field";
import { PaginationControls } from "../components/PaginationControls";
import { Toggle } from "../components/Toggle";
import { api } from "../api";
import type { ProductAttribute, AttributeValue, Pagination } from "../types";
import { normalizeAttributeCode } from "../utils/validation";

export function Attributes() {
  const [tenantId, setTenantId] = useState("");
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

  async function loadAttributes(nextPage = page) {
    if (!tenantId.trim()) {
      setMessage("TenantId is required.");
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
      setMessage(err instanceof Error ? err.message : "Failed to load attributes.");
    } finally {
      setIsLoading(false);
    }
  }

  async function createAttribute() {
    if (!name.trim() || !code.trim()) {
      setMessage("Name and Code are required.");
      return;
    }
    try {
      await api.catalog.attributes.create({
        tenantId: tenantId.trim(),
        name: name.trim(),
        code: normalizeAttributeCode(code),
        attributeType: attrType,
        isVariantAttribute: isVariant,
        isRequired,
        sortOrder: parseInt(sortOrder) || 1,
      });
      setMessage("Attribute created.");
      setName("");
      setCode("");
      await loadAttributes();
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : "Failed to create attribute.");
    }
  }

  async function disableAttribute(id: string) {
    try {
      await api.catalog.attributes.disable(id, tenantId.trim());
      setMessage("Attribute disabled.");
      if (selectedAttrId === id) {
        setSelectedAttrId(null);
        setValues([]);
      }
      await loadAttributes();
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : "Failed to disable attribute.");
    }
  }

  async function loadValues(attrId: string) {
    setSelectedAttrId(attrId);
    setValName("");
    setValCode("");
    setValHex("");
    try {
      const data = await api.catalog.attributes.values.list(attrId, tenantId.trim());
      setValues(data.values);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : "Failed to load values.");
    }
  }

  async function createValue() {
    if (!selectedAttrId || !valName.trim() || !valCode.trim()) {
      setMessage("Value name and code are required.");
      return;
    }
    try {
      await api.catalog.attributes.values.create(selectedAttrId, {
        tenantId: tenantId.trim(),
        productAttributeId: selectedAttrId,
        name: valName.trim(),
        code: normalizeAttributeCode(valCode),
        hexCode: valHex.trim() || null,
        sortOrder: values.length + 1,
      });
      setMessage("Value created.");
      setValName("");
      setValCode("");
      setValHex("");
      await loadValues(selectedAttrId);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : "Failed to create value.");
    }
  }

  async function disableValue(valueId: string) {
    if (!selectedAttrId) return;
    try {
      await api.catalog.attributes.values.disable(selectedAttrId, valueId, tenantId.trim());
      await loadValues(selectedAttrId);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : "Failed to disable value.");
    }
  }

  const attributeTypes = ["Select", "MultiSelect", "Text", "Number", "Boolean", "Date", "Decimal"];

  return (
    <section className="stores-panel">
      <div className="panel-section">
        <div className="section-heading">
          <div>
            <h2>Attributes</h2>
            <p>Manage product attributes and their values.</p>
          </div>
          <button className="secondary" onClick={() => loadAttributes(1)} disabled={isLoading}>
            {isLoading ? "Loading..." : "Load Attributes"}
          </button>
        </div>
        <div className="form-grid">
          <Field label="TenantId" value={tenantId} required onChange={setTenantId} />
          <Field label="Search" value={search} onChange={setSearch} />
        </div>
        {message && <div className="notice error">{message}</div>}
      </div>

      <div className="panel-section">
        <div className="section-heading">
          <div>
            <h2>New Attribute</h2>
          </div>
        </div>
        <div className="form-grid">
          <Field label="Name" value={name} required onChange={setName} />
          <Field label="Code" value={code} required onChange={(v) => setCode(normalizeAttributeCode(v))} />
          <label className="field">
            <span>Type *</span>
            <select value={attrType} onChange={(e) => setAttrType(e.target.value)}>
              {attributeTypes.map((t) => (
                <option key={t} value={t}>{t}</option>
              ))}
            </select>
          </label>
          <Field label="Sort Order" value={sortOrder} onChange={setSortOrder} />
          <Toggle label="Variant Attribute" checked={isVariant} onChange={setIsVariant} />
          <Toggle label="Required" checked={isRequired} onChange={setIsRequired} />
        </div>
        <div className="actions left">
          <button className="primary" onClick={createAttribute}>Create</button>
        </div>
      </div>

      <div className="table-shell">
        <table>
          <thead>
            <tr>
              <th>Name</th>
              <th>Code</th>
              <th>Type</th>
              <th>Variant</th>
              <th>Order</th>
              <th>Status</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {attributes.length === 0 ? (
              <tr><td colSpan={7}>No attributes loaded.</td></tr>
            ) : (
              attributes.map((attr) => (
                <tr key={attr.id}>
                  <td>{attr.name}</td>
                  <td>{attr.code}</td>
                  <td>{attr.attributeType}</td>
                  <td>{attr.isVariantAttribute ? "Yes" : "No"}</td>
                  <td>{attr.sortOrder}</td>
                  <td>{attr.isActive ? "Active" : "Disabled"}</td>
                  <td>
                    <div className="row-actions">
                      <button className="secondary" onClick={() => loadValues(attr.id)}>
                        Values
                      </button>
                      {attr.isActive && (
                        <button className="secondary" onClick={() => disableAttribute(attr.id)}>
                          Disable
                        </button>
                      )}
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
              <h2>Attribute Values</h2>
              <p>Manage values for the selected attribute.</p>
            </div>
          </div>
          <div className="form-grid">
            <Field label="Value Name" value={valName} required onChange={setValName} />
            <Field label="Value Code" value={valCode} required onChange={(v) => setValCode(normalizeAttributeCode(v))} />
            <Field label="Hex Color" value={valHex} onChange={setValHex} placeholder="#000000" />
          </div>
          <div className="actions left">
            <button className="primary" onClick={createValue}>Add Value</button>
          </div>
          <div className="table-shell" style={{ marginTop: "1rem" }}>
            <table>
              <thead>
                <tr>
                  <th>Name</th>
                  <th>Code</th>
                  <th>Color</th>
                  <th>Order</th>
                  <th>Status</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {values.length === 0 ? (
                  <tr><td colSpan={6}>No values for this attribute.</td></tr>
                ) : (
                  values.map((v) => (
                    <tr key={v.id}>
                      <td>{v.name}</td>
                      <td>{v.code}</td>
                      <td>{v.hexCode ? <span style={{ background: v.hexCode, padding: "2px 8px", borderRadius: 4, color: "#fff" }}>{v.hexCode}</span> : "-"}</td>
                      <td>{v.sortOrder}</td>
                      <td>{v.isActive ? "Active" : "Disabled"}</td>
                      <td>
                        {v.isActive && (
                          <button className="secondary" onClick={() => disableValue(v.id)}>Disable</button>
                        )}
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </section>
  );
}
