import { useEffect, useState } from "react";
import { CheckCircle2, Edit3, Plus, PowerOff } from "lucide-react";
import { TenantSetupWizard } from "../components/TenantSetupWizard";
import { ConfirmDialog, EntityModal, PageHeader, SearchInput } from "../components/Crud";
import { PaginationControls } from "../components/PaginationControls";
import { api } from "../api";
import { useI18n } from "../i18n";
import type { TenantDetails, TenantSummary, Pagination } from "../types";

export function Tenants() {
  const { t } = useI18n();
  const [tenants, setTenants] = useState<TenantSummary[]>([]);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [pagination, setPagination] = useState<Pagination | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [isWizardOpen, setIsWizardOpen] = useState(false);
  const [editingTenant, setEditingTenant] = useState<TenantDetails | null>(null);
  const [tenantToDisable, setTenantToDisable] = useState<TenantSummary | null>(null);
  const [tenantToActivate, setTenantToActivate] = useState<TenantSummary | null>(null);

  useEffect(() => {
    void loadTenants(1);
  }, []);

  async function loadTenants(nextPage = page) {
    setIsLoading(true);
    setMessage(null);
    try {
      const data = await api.tenants.list({ page: nextPage, search });
      setTenants(data.tenants ?? []);
      setPagination(data.pagination ?? null);
      setPage(data.pagination?.page ?? nextPage);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("tenants.loadFailed"));
    } finally {
      setIsLoading(false);
    }
  }

  async function openEditTenant(tenantId: string) {
    setMessage(null);
    try {
      const details = await api.tenants.get(tenantId);
      setEditingTenant(details);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("tenants.detailsLoadFailed"));
    }
  }

  async function afterTenantSaved() {
    await loadTenants(page);
    if (editingTenant?.id) {
      const details = await api.tenants.get(editingTenant.id);
      setEditingTenant(details);
    }
  }

  async function disableTenant(tenant: TenantSummary) {
    setMessage(null);
    try {
      await api.tenants.disable(tenant.id);
      setTenantToDisable(null);
      await loadTenants(page);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("tenants.disableFailed"));
    }
  }

  async function activateTenant(tenant: TenantSummary) {
    setMessage(null);
    try {
      await api.tenants.activate(tenant.id);
      setTenantToActivate(null);
      await loadTenants(page);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("tenants.activateFailed"));
    }
  }

  return (
    <section className="tenants-panel">
      <div className="panel-section">
        <PageHeader
          title={t("tenants.title")}
          description={t("tenants.description")}
          actions={<button className="primary" type="button" onClick={() => setIsWizardOpen(true)}><Plus size={16} />{t("common.addNew")}</button>}
        />
        <div className="tenant-search-panel">
          <SearchInput value={search} onChange={setSearch} onSearch={() => loadTenants(1)} isLoading={isLoading} />
        </div>
        {message && <div className="notice error">{message}</div>}
      </div>

      <div className="panel-section">
        <div className="table-shell">
          <table>
            <thead>
              <tr>
                <th>{t("common.name")}</th>
                <th>Slug</th>
                <th>{t("tenants.stores")}</th>
                <th>{t("tenants.currency")}</th>
                <th>{t("common.status")}</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {tenants.length === 0 ? (
                <tr><td colSpan={6}>{t("tenants.noTenantsLoaded")}</td></tr>
              ) : (
                tenants.map((tenant) => (
                  <tr key={tenant.id}>
                    <td>
                      <strong>{tenant.name}</strong>
                      {tenant.primaryStoreId && <small className="table-subtext">{t("tenants.primaryStoreSelected")}</small>}
                    </td>
                    <td>{tenant.slug}</td>
                    <td>{tenant.storeCount}</td>
                    <td>{tenant.currency}</td>
                    <td>{tenant.isActive ? t("common.active") : t("common.inactive")}</td>
                    <td>
                      <div className="row-actions">
                        <button className="secondary" type="button" onClick={() => openEditTenant(tenant.id)}>
                          <Edit3 size={16} />
                          {t("common.edit")}
                        </button>
                        {tenant.isActive ? (
                          <button className="secondary" type="button" onClick={() => setTenantToDisable(tenant)}>
                            <PowerOff size={16} />
                            {t("common.disable")}
                          </button>
                        ) : (
                          <button className="secondary" type="button" onClick={() => setTenantToActivate(tenant)}>
                            <CheckCircle2 size={16} />
                            {t("common.activate")}
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
        <PaginationControls pagination={pagination} onPageChange={loadTenants} />
      </div>

      {isWizardOpen && (
        <EntityModal
          title={t("tenants.newSetup")}
          size="wide"
          onClose={() => setIsWizardOpen(false)}
        >
          <TenantSetupWizard onCompleted={() => { void loadTenants(1); }} />
        </EntityModal>
      )}
      {editingTenant && (
        <EntityModal
          title={t("tenants.editTitle").replace("{name}", editingTenant.name)}
          size="wide"
          onClose={() => setEditingTenant(null)}
        >
          <TenantSetupWizard
            mode="edit"
            tenant={editingTenant}
            onCompleted={() => { void afterTenantSaved(); }}
          />
        </EntityModal>
      )}
      {tenantToDisable && (
        <ConfirmDialog
          title={t("tenants.disableTitle")}
          message={t("tenants.disableMessage").replace("{name}", tenantToDisable.name)}
          confirmLabel={t("common.disable")}
          onCancel={() => setTenantToDisable(null)}
          onConfirm={() => disableTenant(tenantToDisable)}
        />
      )}
      {tenantToActivate && (
        <ConfirmDialog
          title={t("tenants.activateTitle")}
          message={t("tenants.activateMessage").replace("{name}", tenantToActivate.name)}
          confirmLabel={t("common.activate")}
          onCancel={() => setTenantToActivate(null)}
          onConfirm={() => activateTenant(tenantToActivate)}
        />
      )}
    </section>
  );
}
