import { useEffect, useState } from "react";
import { RefreshCw } from "lucide-react";
import { PageHeader } from "../components/Crud";
import { api } from "../api";
import { useAppContext } from "../context/AppContext";
import { useI18n } from "../i18n";
import type { DashboardSummary } from "../types";

export function Dashboard() {
  const { currentTenant, currentStore } = useAppContext();
  const { t } = useI18n();
  const [summary, setSummary] = useState<DashboardSummary | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    void loadSummary();
  }, [currentTenant?.id, currentStore?.id]);

  async function loadSummary() {
    setIsLoading(true);
    setMessage(null);
    try {
      setSummary(await api.dashboard.summary());
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("dashboard.loadFailed"));
    } finally {
      setIsLoading(false);
    }
  }

  return (
    <section className="dashboard-page">
      <div className="panel-section">
        <PageHeader
          title={t("dashboard.title")}
          description={t("dashboard.description")}
          actions={(
            <button className="secondary" type="button" onClick={loadSummary} disabled={isLoading}>
              <RefreshCw size={16} />
              {isLoading ? t("common.loading") : t("common.refresh")}
            </button>
          )}
        />

        <div className="dashboard-context">
          <span>{t("dashboard.scope")}: {scopeLabel(summary?.scope, t)}</span>
          {currentTenant && <span>{currentTenant.name}</span>}
          {currentStore && <span>{currentStore.name}</span>}
        </div>

        {message && <div className="notice error">{message}</div>}
      </div>

      {summary && (
        <>
          <div className="dashboard-metrics">
            {summary.metrics.filter((metric) => metric.key !== "tenants").map((metric) => (
              <article key={metric.key} className="metric">
                <span>{metricLabel(metric.key, metric.label, t)}</span>
                <strong>{formatNumber(metric.value)}</strong>
                {metric.hint && <small>{metric.hint}</small>}
              </article>
            ))}
          </div>

          <div className="dashboard-content-grid">
            <section className="panel-section">
              <PageHeader title={t("dashboard.inventorySnapshot")} />
              <div className="dashboard-inventory-grid">
                <InventoryStat label={t("dashboard.totalQuantity")} value={summary.inventory.totalQuantity} />
                <InventoryStat label={t("dashboard.availableQuantity")} value={summary.inventory.availableQuantity} />
                <InventoryStat label={t("dashboard.reservedQuantity")} value={summary.inventory.reservedQuantity} />
                <InventoryStat label={t("dashboard.lowStockProducts")} value={summary.inventory.lowStockProducts} warn />
                <InventoryStat label={t("dashboard.outOfStockProducts")} value={summary.inventory.outOfStockProducts} danger />
              </div>
            </section>

            <section className="panel-section">
              <PageHeader title={t("dashboard.recentMovements")} />
              {summary.recentMovements.length === 0 ? (
                <div className="empty-state">{t("dashboard.noMovements")}</div>
              ) : (
                <div className="dashboard-movement-list">
                  {summary.recentMovements.map((movement) => (
                    <article key={movement.id} className="dashboard-movement">
                      <div>
                        <strong>{movement.productName}</strong>
                        <span>{movement.variantSku} · {movement.type}</span>
                      </div>
                      <div>
                        <strong>{formatNumber(movement.quantity)}</strong>
                        <span>{movement.storeName ?? "-"}</span>
                      </div>
                    </article>
                  ))}
                </div>
              )}
            </section>
          </div>
        </>
      )}
    </section>
  );
}

function InventoryStat({
  label,
  value,
  warn = false,
  danger = false,
}: {
  label: string;
  value: number;
  warn?: boolean;
  danger?: boolean;
}) {
  const className = danger ? "dashboard-stat danger" : warn ? "dashboard-stat warn" : "dashboard-stat";
  return (
    <div className={className}>
      <span>{label}</span>
      <strong>{formatNumber(value)}</strong>
    </div>
  );
}

function metricLabel(key: string, fallback: string, t: (key: string) => string) {
  const translated = t(`dashboard.metric.${key}`);
  return translated.startsWith("dashboard.metric.") ? fallback : translated;
}

function scopeLabel(scope: string | undefined, t: (key: string) => string) {
  if (scope === "Platform") return t("dashboard.platformScope");
  if (scope === "Tenant") return t("dashboard.tenantScope");
  if (scope === "Store") return t("dashboard.storeScope");
  return "-";
}

function formatNumber(value: number) {
  return new Intl.NumberFormat().format(value);
}
