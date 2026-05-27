import { useState } from "react";
import { Field } from "../components/Field";
import { PaginationControls } from "../components/PaginationControls";
import { api } from "../api";
import type { ProductType, Pagination } from "../types";

export function ProductTypes() {
  const [tenantId, setTenantId] = useState("");
  const [types, setTypes] = useState<ProductType[]>([]);
  const [message, setMessage] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [pagination, setPagination] = useState<Pagination | null>(null);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");

  async function loadTypes(nextPage = page) {
    if (!tenantId.trim()) {
      setMessage("TenantId is required.");
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
      setMessage(err instanceof Error ? err.message : "Failed to load product types.");
    } finally {
      setIsLoading(false);
    }
  }

  async function createType() {
    if (!name.trim()) {
      setMessage("Name is required.");
      return;
    }
    try {
      await api.catalog.productTypes.create({
        tenantId: tenantId.trim(),
        name: name.trim(),
        description: description.trim() || null,
      });
      setMessage("Product type created.");
      setName("");
      setDescription("");
      await loadTypes();
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : "Failed to create product type.");
    }
  }

  async function disableType(id: string) {
    try {
      await api.catalog.productTypes.disable(id, tenantId.trim());
      setMessage("Product type disabled.");
      await loadTypes();
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : "Failed to disable product type.");
    }
  }

  return (
    <section className="stores-panel">
      <div className="panel-section">
        <div className="section-heading">
          <div>
            <h2>Product Types</h2>
            <p>Manage product types for catalog organization.</p>
          </div>
          <button className="secondary" onClick={() => loadTypes(1)} disabled={isLoading}>
            {isLoading ? "Loading..." : "Load Types"}
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
            <h2>New Product Type</h2>
          </div>
        </div>
        <div className="form-grid">
          <Field label="Name" value={name} required onChange={setName} />
          <Field label="Description" value={description} onChange={setDescription} />
        </div>
        <div className="actions left">
          <button className="primary" onClick={createType}>
            Create
          </button>
        </div>
      </div>

      <div className="table-shell">
        <table>
          <thead>
            <tr>
              <th>Name</th>
              <th>Description</th>
              <th>Status</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {types.length === 0 ? (
              <tr>
                <td colSpan={4}>No product types loaded.</td>
              </tr>
            ) : (
              types.map((pt) => (
                <tr key={pt.id}>
                  <td>{pt.name}</td>
                  <td>{pt.description || "-"}</td>
                  <td>{pt.isActive ? "Active" : "Disabled"}</td>
                  <td>
                    {pt.isActive && (
                      <button className="secondary" onClick={() => disableType(pt.id)}>
                        Disable
                      </button>
                    )}
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
        <PaginationControls pagination={pagination} onPageChange={(nextPage) => loadTypes(nextPage)} />
      </div>
    </section>
  );
}
