import {
  Boxes,
  Building2,
  KeyRound,
  LayoutDashboard,
  Package,
  Shield,
  Store,
  Users,
  Tags,
  ListOrdered,
  SlidersHorizontal,
  LogOut,
} from "lucide-react";

const csrfToken =
  document.querySelector<HTMLMetaElement>('meta[name="arca-csrf-token"]')
    ?.content ?? "";
const userName =
  document.querySelector<HTMLMetaElement>('meta[name="arca-user-name"]')
    ?.content ?? "Admin";

export type NavItem = {
  id: string;
  label: string;
  icon: React.ComponentType<{ size?: number }>;
};

export const navItems: NavItem[] = [
  { id: "dashboard", label: "Dashboard", icon: LayoutDashboard },
  { id: "tenants", label: "Tenants", icon: Building2 },
  { id: "stores", label: "Stores", icon: Store },
  { id: "users", label: "Users", icon: Users },
  { id: "categories", label: "Categories", icon: Tags },
  { id: "product-types", label: "Product Types", icon: ListOrdered },
  { id: "attributes", label: "Attributes", icon: SlidersHorizontal },
  { id: "products", label: "Products", icon: Package },
  { id: "inventory", label: "Inventory", icon: Boxes },
  { id: "integrations", label: "API Keys", icon: KeyRound },
  { id: "roles", label: "Roles", icon: Shield },
];

export function Sidebar({
  active,
  onNavigate,
}: {
  active: string;
  onNavigate: (id: string) => void;
}) {
  return (
    <aside className="sidebar">
      <div className="brand">
        <div className="brand-mark">A</div>
        <div>
          <strong>Arca</strong>
          <span>Inventory SaaS</span>
        </div>
      </div>
      <nav>
        {navItems.map((item) => {
          const Icon = item.icon;
          return (
            <button
              key={item.id}
              className={active === item.id ? "nav-item active" : "nav-item"}
              onClick={() => onNavigate(item.id)}
              title={item.label}
            >
              <Icon size={18} />
              <span>{item.label}</span>
            </button>
          );
        })}
      </nav>
      <div style={{ marginTop: "auto" }}>
        <form method="post" action="/logout">
          <input
            type="hidden"
            name="__RequestVerificationToken"
            value={csrfToken}
          />
          <button className="nav-item" type="submit" title="Sign out">
            <LogOut size={18} />
            <span>{userName}</span>
          </button>
        </form>
      </div>
    </aside>
  );
}
