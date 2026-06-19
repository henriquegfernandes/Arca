import { Check, ChevronDown, ChevronRight, Languages, LogOut, Menu, UserRound } from "lucide-react";
import { useState } from "react";
import arcaLogo from "../assets/arca-logo.svg";
import { useAppContext } from "../context/AppContext";
import { useI18n } from "../i18n";

const csrfToken =
  document.querySelector<HTMLMetaElement>('meta[name="arca-csrf-token"]')
    ?.content ?? "";

export function AppHeader({
  activePage,
  onOpenNavigation,
}: {
  activePage: string;
  onOpenNavigation?: () => void;
}) {
  const appContext = useAppContext();
  const { t } = useI18n();
  void activePage;
  const isSuperAdmin = appContext.currentUser?.isSuperAdmin === true;
  const canShowStore = appContext.currentTenant;

  return (
    <header className="app-header">
      <div className="header-brand">
        {onOpenNavigation && (
          <button className="mobile-menu-button" type="button" onClick={onOpenNavigation} aria-label={t("header.openMenu")}>
            <Menu size={20} />
          </button>
        )}
        <img className="header-logo" src={arcaLogo} alt="Arca Inventory Management Platform" />
      </div>

      <div className="header-context">
        {appContext.isLoading && <span className="context-pill">{t("header.loadingContext")}</span>}
        {appContext.error && <span className="context-pill error">{appContext.error}</span>}
        {isSuperAdmin ? (
          <TenantSelector />
        ) : (
          appContext.currentTenant && <span className="tenant-context-pill">{appContext.currentTenant.name}</span>
        )}
        {canShowStore && <StoreSelector />}
        {isSuperAdmin && !appContext.currentTenant && (
          <span className="context-pill">{t("header.selectTenant")}</span>
        )}
      </div>

      <UserMenu />
    </header>
  );
}

function TenantSelector() {
  const { currentTenant, availableTenants, selectTenant } = useAppContext();
  const { t } = useI18n();
  const sortedTenants = [...availableTenants].sort((a, b) => a.name.localeCompare(b.name));

  return (
    <label className="context-select inline">
      <span>{t("header.tenant")}</span>
      <select
        value={currentTenant?.id ?? ""}
        onChange={(event) => {
          const nextTenantId = event.target.value;
          if (!nextTenantId) {
            void selectTenant(null);
            return;
          }

          if (nextTenantId !== currentTenant?.id) {
            void selectTenant(nextTenantId);
          }
        }}
      >
        <option value="">{t("header.selectTenant")}</option>
        {sortedTenants.map((tenant) => (
          <option key={tenant.id} value={tenant.id}>{tenant.name}</option>
        ))}
      </select>
    </label>
  );
}

function StoreSelector() {
  const { currentStore, availableStores, selectStore } = useAppContext();
  const { t } = useI18n();
  const sortedStores = [...availableStores].sort((a, b) => a.name.localeCompare(b.name));

  if (availableStores.length === 0) {
    return <span className="context-pill">{t("header.noStores")}</span>;
  }

  return (
    <label className="context-select inline">
      <span>{t("header.store")}</span>
      <select
        value={currentStore?.id ?? ""}
        onChange={(event) => {
          const nextStoreId = event.target.value;
          if (!nextStoreId) {
            void selectStore(null);
            return;
          }

          if (nextStoreId !== currentStore?.id) {
            void selectStore(nextStoreId);
          }
        }}
      >
        <option value="">{t("header.allStores")}</option>
        {sortedStores.map((store) => (
          <option key={store.id} value={store.id}>{store.name}</option>
        ))}
      </select>
    </label>
  );
}

function UserMenu() {
  const { currentUser } = useAppContext();
  const { language, setLanguage, t } = useI18n();
  const [isOpen, setIsOpen] = useState(false);
  const [isLanguageOpen, setIsLanguageOpen] = useState(false);

  const changeLanguage = (nextLanguage: "en-US" | "pt-BR") => {
    setLanguage(nextLanguage);
    setIsLanguageOpen(false);
  };

  return (
    <div className="user-menu">
      <button className="user-menu-trigger" type="button" onClick={() => setIsOpen((value) => !value)}>
        <UserRound size={18} />
        <span>
          <strong>{currentUser?.fullName ?? t("header.userFallback")}</strong>
          <small>{currentUser?.roles[0] ?? t("header.authenticated")}</small>
        </span>
      </button>
      {isOpen && (
        <div className="user-menu-popover">
          <button type="button" className="user-menu-item" disabled>{t("profile.profile")}</button>
          <button
            type="button"
            className="user-menu-item user-menu-item-spread"
            aria-expanded={isLanguageOpen}
            onClick={() => setIsLanguageOpen((value) => !value)}
          >
            <span className="user-menu-item-label">
              <Languages size={16} />
              {t("profile.language")}
            </span>
            {isLanguageOpen ? <ChevronDown size={16} /> : <ChevronRight size={16} />}
          </button>
          {isLanguageOpen && (
            <div className="user-menu-language-options">
              <button
                type="button"
                className={language === "en-US" ? "user-menu-language-option active" : "user-menu-language-option"}
                onClick={() => changeLanguage("en-US")}
              >
                <span>{t("profile.english")}</span>
                {language === "en-US" && <Check size={16} />}
              </button>
              <button
                type="button"
                className={language === "pt-BR" ? "user-menu-language-option active" : "user-menu-language-option"}
                onClick={() => changeLanguage("pt-BR")}
              >
                <span>{t("profile.portuguese")}</span>
                {language === "pt-BR" && <Check size={16} />}
              </button>
            </div>
          )}
          <form method="post" action="/logout">
            <input type="hidden" name="__RequestVerificationToken" value={csrfToken} />
            <button type="submit" className="user-menu-item danger">
              <LogOut size={16} />
              {t("profile.logout")}
            </button>
          </form>
        </div>
      )}
    </div>
  );
}
