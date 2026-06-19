import { useEffect, useState, type FormEvent } from "react";
import { api } from "../api";
import { PageHeader } from "../components/Crud";
import { PaginationControls } from "../components/PaginationControls";
import { useAppContext } from "../context/AppContext";
import { useI18n } from "../i18n";
import type { AuditLogEntry, Pagination } from "../types";

export function AuditLogs() {
  const { t } = useI18n();
  const { currentTenant, currentStore } = useAppContext();
  const [logs, setLogs] = useState<AuditLogEntry[]>([]);
  const [pagination, setPagination] = useState<Pagination | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [search, setSearch] = useState("");
  const [entityFilter, setEntityFilter] = useState("");
  const [actionFilter, setActionFilter] = useState("");
  const [page, setPage] = useState(1);

  const load = async (nextPage = page) => {
    if (!currentTenant?.id) {
      setLogs([]);
      setPagination(null);
      setLoading(false);
      setError(t("auditLogs.selectTenant"));
      return;
    }

    setLoading(true);
    setError("");
    try {
      const result = await api.auditLogs.list({
        tenantId: currentTenant.id,
        storeId: currentStore?.id,
        page: nextPage,
        pageSize: 25,
        search: search || undefined,
        entityName: entityFilter || undefined,
        action: actionFilter || undefined,
      });
      setLogs(result.logs);
      setPagination(result.pagination ?? null);
      setPage(result.pagination?.page ?? nextPage);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : t("auditLogs.loadFailed"));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(page); }, [page, currentTenant?.id, currentStore?.id]);

  const handleSearch = (e: FormEvent) => {
    e.preventDefault();
    setPage(1);
    load(1);
  };

  const entityNames = [...new Set(logs.map((l) => l.entityName))];
  const actions = [...new Set(logs.map((l) => l.action))];

  return (
    <section className="stores-panel">
      <div className="panel-section">
        <PageHeader
          title={t("auditLogs.title")}
          description={t("auditLogs.description")}
        />

        <div className="context-hint">
          {currentTenant ? `${t("header.tenant")}: ${currentTenant.name}` : t("auditLogs.selectTenant")}
          {currentStore ? ` · ${t("header.store")}: ${currentStore.name}` : ""}
        </div>

        <form className="filter-bar audit-filter-bar" onSubmit={handleSearch}>
          <label className="search-input">
            <span>{t("common.search")}</span>
            <input
              type="text"
              placeholder={t("auditLogs.searchPlaceholder")}
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </label>
          <label className="field">
            <span>{t("auditLogs.entity")}</span>
            <select value={entityFilter} onChange={(e) => setEntityFilter(e.target.value)}>
              <option value="">{t("auditLogs.allEntities")}</option>
              {entityNames.map((n) => (
                <option key={n} value={n}>{n}</option>
              ))}
            </select>
          </label>
          <label className="field">
            <span>{t("auditLogs.action")}</span>
            <select value={actionFilter} onChange={(e) => setActionFilter(e.target.value)}>
              <option value="">{t("auditLogs.allActions")}</option>
              {actions.map((a) => (
                <option key={a} value={a}>{a}</option>
              ))}
            </select>
          </label>
          <button type="submit" className="secondary" disabled={loading}>
            {loading ? t("auditLogs.filtering") : t("auditLogs.filter")}
          </button>
        </form>

        {error && <div className="notice error">{error}</div>}
      </div>

      {loading ? (
        <div className="panel-section">{t("common.loading")}...</div>
      ) : (
        <div className="table-shell">
          <table>
            <thead>
              <tr>
                <th>{t("auditLogs.date")}</th>
                <th>{t("auditLogs.action")}</th>
                <th>{t("auditLogs.entity")}</th>
                <th>{t("auditLogs.entityId")}</th>
                <th>{t("auditLogs.oldValue")}</th>
                <th>{t("auditLogs.newValue")}</th>
                <th>{t("auditLogs.user")}</th>
                <th>{t("auditLogs.tenant")}</th>
              </tr>
            </thead>
            <tbody>
              {logs.length === 0 ? (
                <tr><td colSpan={8}>{t("auditLogs.noLogsFound")}</td></tr>
              ) : (
                logs.map((log) => (
                  <tr key={log.id}>
                    <td>{new Date(log.createdAt).toLocaleString()}</td>
                    <td><code className="status-badge">{log.action}</code></td>
                    <td>{log.entityName}</td>
                    <td className="mono-cell">{log.entityId ? `${log.entityId.slice(0, 8)}...` : "-"}</td>
                    <td className="mono-cell truncate-cell" title={log.oldValue ?? undefined}>{log.oldValue ?? "-"}</td>
                    <td className="mono-cell truncate-cell" title={log.newValue ?? undefined}>{log.newValue ?? "-"}</td>
                    <td className="mono-cell">{log.userId ? `${log.userId.slice(0, 8)}...` : t("auditLogs.system")}</td>
                    <td className="mono-cell">{log.tenantId ? `${log.tenantId.slice(0, 8)}...` : "-"}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
          <PaginationControls pagination={pagination} onPageChange={setPage} />
        </div>
      )}
    </section>
  );
}
