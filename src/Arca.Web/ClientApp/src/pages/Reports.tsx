import { useState } from "react";
import { useAppContext } from "../context/AppContext";
import { PageHeader } from "../components/Crud";
import { useI18n } from "../i18n";
import { Download } from "lucide-react";

const csrfToken =
  document.querySelector<HTMLMetaElement>('meta[name="arca-csrf-token"]')
    ?.content ?? "";

export function Reports() {
  const { currentTenant, currentStore } = useAppContext();
  const { t } = useI18n();
  const tenantId = currentTenant?.id ?? "";
  const storeId = currentStore?.id ?? "";
  const [loading, setLoading] = useState("");
  const [error, setError] = useState("");

  const download = async (url: string, fileName: string, loadingKey: string) => {
    setLoading(loadingKey);
    setError("");
    try {
      const response = await fetch(url, {
        headers: {
          ...(csrfToken ? { RequestVerificationToken: csrfToken } : {}),
          ...(tenantId ? { "X-Tenant-Id": tenantId } : {}),
          ...(storeId ? { "X-Store-Id": storeId } : {}),
        },
      });
      if (!response.ok) {
        const payload = await response.json().catch(() => null);
        throw new Error(payload?.error ?? t("reports.downloadFailed"));
      }
      const blob = await response.blob();
      const a = document.createElement("a");
      a.href = URL.createObjectURL(blob);
      a.download = fileName;
      a.click();
      URL.revokeObjectURL(a.href);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : t("reports.downloadFailed"));
    } finally {
      setLoading("");
    }
  };

  return (
    <section className="stores-panel">
      <div className="panel-section">
        <PageHeader
          title={t("reports.title")}
          description={t("reports.description")}
        />

        <div className="report-context">
          <div className={currentTenant ? "context-pill" : "context-pill error"}>
            {currentTenant ? `${t("auditLogs.tenant")}: ${currentTenant.name}` : t("reports.selectTenant")}
          </div>
          <div className={currentStore ? "context-pill" : "context-pill error"}>
            {currentStore ? `${t("header.store")}: ${currentStore.name}` : t("reports.selectStore")}
          </div>
        </div>

        {error && <div className="notice error">{error}</div>}
      </div>

      <div className="report-grid">
        <ReportCard
          title={t("reports.products")}
          description={t("reports.productsDescription")}
          disabled={!tenantId || loading !== ""}
          loading={loading === "products"}
          onClick={() => download(`/api/admin/reports/products.csv?tenantId=${tenantId}`, `products-${new Date().toISOString().slice(0, 10)}.csv`, "products")}
        />
        <ReportCard
          title={t("reports.inventory")}
          description={t("reports.inventoryDescription")}
          disabled={!tenantId || !storeId || loading !== ""}
          loading={loading === "inventory"}
          onClick={() => download(`/api/admin/reports/inventory.csv?tenantId=${tenantId}&storeId=${storeId}`, `inventory-${new Date().toISOString().slice(0, 10)}.csv`, "inventory")}
        />
        <ReportCard
          title={t("reports.movements")}
          description={t("reports.movementsDescription")}
          disabled={!tenantId || !storeId || loading !== ""}
          loading={loading === "movements"}
          onClick={() => download(`/api/admin/reports/movements.csv?tenantId=${tenantId}&storeId=${storeId}`, `movements-${new Date().toISOString().slice(0, 10)}.csv`, "movements")}
        />
      </div>
    </section>
  );
}

function ReportCard({
  title,
  description,
  disabled,
  loading,
  onClick,
}: {
  title: string;
  description: string;
  disabled: boolean;
  loading: boolean;
  onClick: () => void;
}) {
  const { t } = useI18n();

  return (
    <article className="report-card">
      <div>
        <strong>{title}</strong>
        <span>{description}</span>
      </div>
      <button className="primary" disabled={disabled} onClick={onClick}>
        <Download size={16} />
        {loading ? t("reports.downloading") : t("reports.exportCsv")}
      </button>
    </article>
  );
}
