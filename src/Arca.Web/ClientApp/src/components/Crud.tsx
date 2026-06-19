import type { ReactNode } from "react";
import { CheckCircle2, Search, X } from "lucide-react";
import { useI18n } from "../i18n";

export function PageHeader({
  title,
  description,
  actions,
}: {
  title: string;
  description?: string;
  actions?: ReactNode;
}) {
  return (
    <div className="crud-header">
      <div>
        <h2>{title}</h2>
        {description && <p>{description}</p>}
      </div>
      {actions && <div className="crud-header-actions">{actions}</div>}
    </div>
  );
}

export function SearchInput({
  value,
  onChange,
  onSearch,
  isLoading,
  placeholder = "Search",
}: {
  value: string;
  onChange: (value: string) => void;
  onSearch: () => void;
  isLoading?: boolean;
  placeholder?: string;
}) {
  const { t } = useI18n();
  const label = placeholder === "Search" ? t("common.search") : placeholder;
  return (
    <form
      className="filter-bar"
      onSubmit={(event) => {
        event.preventDefault();
        onSearch();
      }}
    >
      <label className="search-input">
        <span>{label}</span>
        <input value={value} onChange={(event) => onChange(event.target.value)} />
      </label>
      <button className="secondary" type="submit" disabled={isLoading}>
        <Search size={16} />
        {isLoading ? t("common.searching") : t("common.search")}
      </button>
    </form>
  );
}

export function EntityModal({
  title,
  children,
  footer,
  onClose,
  size = "default",
}: {
  title: string;
  children: ReactNode;
  footer?: ReactNode;
  onClose: () => void;
  size?: "default" | "wide";
}) {
  return (
    <div className="modal-backdrop" role="presentation" onMouseDown={onClose}>
      <section
        className={size === "wide" ? "entity-modal wide" : "entity-modal"}
        role="dialog"
        aria-modal="true"
        aria-labelledby="entity-modal-title"
        onMouseDown={(event) => event.stopPropagation()}
      >
        <div className="modal-heading">
          <h2 id="entity-modal-title">{title}</h2>
          <button className="icon-button" type="button" onClick={onClose} aria-label="Close modal">
            <X size={18} />
          </button>
        </div>
        <div className="modal-body">{children}</div>
        {footer && <div className="modal-footer">{footer}</div>}
      </section>
    </div>
  );
}

export function ConfirmDialog({
  title,
  message,
  confirmLabel,
  onConfirm,
  onCancel,
}: {
  title: string;
  message: string;
  confirmLabel?: string;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  const { t } = useI18n();
  const resolvedConfirmLabel = confirmLabel ?? t("common.confirm");
  return (
    <EntityModal title={title} onClose={onCancel} footer={(
      <>
        <button className="secondary" type="button" onClick={onCancel}>{t("common.cancel")}</button>
        <button className="primary danger" type="button" onClick={onConfirm}><CheckCircle2 size={16} />{resolvedConfirmLabel}</button>
      </>
    )}>
      <p className="confirm-message">{message}</p>
    </EntityModal>
  );
}

export function EmptyState({ children }: { children: ReactNode }) {
  return <div className="empty-state">{children}</div>;
}

export function DetailGrid({
  items,
}: {
  items: Array<{ label: string; value: ReactNode }>;
}) {
  return (
    <dl className="detail-grid">
      {items.map((item) => (
        <div key={item.label}>
          <dt>{item.label}</dt>
          <dd>{item.value || "-"}</dd>
        </div>
      ))}
    </dl>
  );
}
