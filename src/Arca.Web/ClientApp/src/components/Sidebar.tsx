import type { ComponentType } from "react";
import {
  Boxes,
  Building2,
  KeyRound,
  LayoutDashboard,
  Shield,
  Store,
  Users,
  Tags,
  ListOrdered,
  SlidersHorizontal,
  ScrollText,
  FileDown,
} from "lucide-react";
import { useI18n } from "../i18n";

export type NavItem = {
  id: string;
  label: string;
  icon: ComponentType<{ size?: number }>;
  group: "main" | "customizations" | "admin" | "super-admin";
  permission?: string;
  superAdminOnly?: boolean;
};

export const navItems: NavItem[] = [
  { id: "dashboard", label: "navigation.dashboard", icon: LayoutDashboard, group: "main" },
  { id: "inventory", label: "navigation.stock", icon: Boxes, permission: "inventory.view", group: "main" },
  { id: "categories", label: "navigation.categories", icon: Tags, permission: "categories.manage", group: "customizations" },
  { id: "product-types", label: "navigation.productTypes", icon: ListOrdered, permission: "product_types.manage", group: "customizations" },
  { id: "attributes", label: "navigation.attributes", icon: SlidersHorizontal, permission: "attributes.manage", group: "customizations" },
  { id: "stores", label: "navigation.stores", icon: Store, permission: "stores.view", group: "admin" },
  { id: "users", label: "navigation.users", icon: Users, permission: "users.view", group: "admin" },
  { id: "roles", label: "navigation.roles", icon: Shield, permission: "roles.view", superAdminOnly: true, group: "admin" },
  { id: "reports", label: "navigation.reports", icon: FileDown, permission: "reports.view", group: "admin" },
  { id: "integrations", label: "navigation.integrations", icon: KeyRound, permission: "api_keys.manage", group: "admin" },
  { id: "tenants", label: "navigation.tenants", icon: Building2, permission: "tenants.view", superAdminOnly: true, group: "super-admin" },
  { id: "audit-logs", label: "navigation.auditLogs", icon: ScrollText, permission: "audit.view", superAdminOnly: true, group: "super-admin" },
];

const navGroups: Array<{ id: Exclude<NavItem["group"], "main">; label: string }> = [
  { id: "customizations", label: "navigation.group.customizations" },
  { id: "admin", label: "navigation.group.admin" },
  { id: "super-admin", label: "navigation.group.superAdmin" },
];

export function Sidebar({
  active,
  collapsed,
  items,
  onNavigate,
  onHoverChange,
  mobileOpen = false,
}: {
  active: string;
  collapsed: boolean;
  items: NavItem[];
  onNavigate: (id: string) => void;
  onHoverChange: (isHovered: boolean) => void;
  mobileOpen?: boolean;
}) {
  const { t } = useI18n();
  const mainItems = items.filter((item) => item.group === "main");

  return (
    <aside
      className={`${collapsed ? "sidebar collapsed" : "sidebar"}${mobileOpen ? " mobile-open" : ""}`}
      onMouseEnter={() => onHoverChange(true)}
      onMouseLeave={() => onHoverChange(false)}
    >
      <nav>
        {mainItems.length > 0 && (
          <div className="nav-main">
            {mainItems.map((item) => {
              const Icon = item.icon;
              return (
                <button
                  key={item.id}
                  className={active === item.id ? "nav-item active" : "nav-item"}
                  onClick={() => onNavigate(item.id)}
                  title={t(item.label)}
                >
                  <Icon size={18} />
                  <span>{t(item.label)}</span>
                </button>
              );
            })}
          </div>
        )}

        {navGroups.map((group) => {
          const groupItems = items.filter((item) => item.group === group.id);
          if (groupItems.length === 0) return null;

          return (
            <div key={group.id} className="nav-group">
              <span className="nav-group-label">{t(group.label)}</span>
              {groupItems.map((item) => {
                const Icon = item.icon;
                return (
                  <button
                    key={item.id}
                    className={active === item.id ? "nav-item active" : "nav-item"}
                    onClick={() => onNavigate(item.id)}
                    title={t(item.label)}
                  >
                    <Icon size={18} />
                    <span>{t(item.label)}</span>
                  </button>
                );
              })}
            </div>
          );
        })}
      </nav>
    </aside>
  );
}
