import { useEffect, useMemo, useState } from "react";
import { AppHeader } from "./components/AppHeader";
import { Sidebar, navItems } from "./components/Sidebar";
import { useAppContext } from "./context/AppContext";
import { Dashboard } from "./pages/Dashboard";
import { Tenants } from "./pages/Tenants";
import { Stores } from "./pages/Stores";
import { Users } from "./pages/Users";
import { Roles } from "./pages/Roles";
import { ApiKeys } from "./pages/ApiKeys";
import { AuditLogs } from "./pages/AuditLogs";
import { Reports } from "./pages/Reports";
import { Categories } from "./pages/Categories";
import { ProductTypes } from "./pages/ProductTypes";
import { Attributes } from "./pages/Attributes";
import { Products } from "./pages/Products";
import { Inventory } from "./pages/Inventory";
import { useI18n } from "./i18n";

export default function App() {
  const [active, setActive] = useState("dashboard");
  const [isSidebarHovered, setIsSidebarHovered] = useState(false);
  const [isMobileNavOpen, setIsMobileNavOpen] = useState(false);
  const appContext = useAppContext();
  const { t } = useI18n();

  const visibleNavItems = useMemo(() => {
    return navItems.filter((item) => {
      if (item.superAdminOnly && appContext.currentUser?.isSuperAdmin !== true) {
        return false;
      }

      return !item.permission || appContext.hasPermission(item.permission);
    });
  }, [appContext]);

  useEffect(() => {
    if (visibleNavItems.length > 0 && !visibleNavItems.some((item) => item.id === active)) {
      setActive(visibleNavItems[0].id);
    }
  }, [active, visibleNavItems]);

  const title = t(visibleNavItems.find((item) => item.id === active)?.label ?? "navigation.dashboard");
  const isSidebarCollapsed = !isSidebarHovered;
  const navigate = (id: string) => {
    setActive(id);
    setIsMobileNavOpen(false);
  };

  return (
    <div className={isSidebarCollapsed ? "app-shell sidebar-collapsed" : "app-shell"}>
      <AppHeader activePage={active} onOpenNavigation={() => setIsMobileNavOpen(true)} />
      {isMobileNavOpen && (
        <button className="mobile-nav-backdrop" type="button" aria-label={t("common.close")} onClick={() => setIsMobileNavOpen(false)} />
      )}
      <Sidebar
        active={active}
        collapsed={isMobileNavOpen ? false : isSidebarCollapsed}
        items={visibleNavItems}
        onNavigate={navigate}
        onHoverChange={setIsSidebarHovered}
        mobileOpen={isMobileNavOpen}
      />

      <main className="workspace">
        <div className="workspace-scroll">
        <header className="page-topbar">
          <div>
            <h1>{title}</h1>
            <p>{t("common.administrativePanel")}</p>
          </div>
        </header>

        {active === "dashboard" && <Dashboard />}
        {active === "tenants" && <Tenants />}
        {active === "stores" && <Stores />}
        {active === "users" && <Users />}
        {active === "categories" && <Categories />}
        {active === "product-types" && <ProductTypes />}
        {active === "attributes" && <Attributes />}
        {active === "products" && <Products />}
        {active === "inventory" && <Inventory />}
        {active === "integrations" && <ApiKeys />}
        {active === "audit-logs" && <AuditLogs />}
        {active === "reports" && <Reports />}
        {active === "roles" && <Roles />}
        </div>
      </main>
    </div>
  );
}
