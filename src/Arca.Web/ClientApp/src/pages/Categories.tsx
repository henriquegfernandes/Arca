import { useState } from "react";
import { Field } from "../components/Field";
import { PaginationControls } from "../components/PaginationControls";
import { api } from "../api";
import type { Category, Pagination } from "../types";

export function Categories() {
  const [tenantId, setTenantId] = useState("");
  const [categories, setCategories] = useState<Category[]>([]);
  const [message, setMessage] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [pagination, setPagination] = useState<Pagination | null>(null);

  async function loadCategories(nextPage = page) {
    if (!tenantId.trim()) {
      setMessage("TenantId is required.");
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
      setMessage(err instanceof Error ? err.message : "Failed to load categories.");
    } finally {
      setIsLoading(false);
    }
  }

  async function disableCategory(id: string) {
    try {
      await api.catalog.categories.disable(id, tenantId.trim());
      setMessage("Category disabled.");
      await loadCategories();
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : "Failed to disable category.");
    }
  }

  return (
    <section className="stores-panel">
      <div className="panel-section">
        <div className="section-heading">
          <div>
            <h2>Categories</h2>
            <p>Manage product categories.</p>
          </div>
          <button className="secondary" onClick={() => loadCategories(1)} disabled={isLoading}>
            {isLoading ? "Loading..." : "Load Categories"}
          </button>
        </div>
        <div className="form-grid">
          <Field label="TenantId" value={tenantId} required onChange={setTenantId} />
          <Field label="Search" value={search} onChange={setSearch} />
        </div>
        {message && <div className="notice error">{message}</div>}
      </div>

      <div className="table-shell">
        <table>
          <thead>
            <tr>
              <th>Name</th>
              <th>Slug</th>
              <th>Order</th>
              <th>Status</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {categories.length === 0 ? (
              <tr>
                <td colSpan={5}>No categories loaded.</td>
              </tr>
            ) : (
              categories.map((cat) => (
                <tr key={cat.id}>
                  <td>{cat.name}</td>
                  <td>{cat.slug}</td>
                  <td>{cat.sortOrder}</td>
                  <td>{cat.isActive ? "Active" : "Disabled"}</td>
                  <td>
                    {cat.isActive && (
                      <button className="secondary" onClick={() => disableCategory(cat.id)}>
                        Disable
                      </button>
                    )}
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
        <PaginationControls pagination={pagination} onPageChange={(nextPage) => loadCategories(nextPage)} />
      </div>
    </section>
  );
}
