import { useState } from "react";
import { Sidebar } from "./components/Sidebar";
import { Dashboard } from "./pages/Dashboard";
import { Tenants } from "./pages/Tenants";
import { Stores } from "./pages/Stores";
import { Users } from "./pages/Users";
import { Roles } from "./pages/Roles";
import { ApiKeys } from "./pages/ApiKeys";
import { Categories } from "./pages/Categories";
import { ProductTypes } from "./pages/ProductTypes";
import { Attributes } from "./pages/Attributes";
import { Products } from "./pages/Products";
import { Inventory } from "./pages/Inventory";

export default function App() {
  const [active, setActive] = useState("tenants");

  const title = active.charAt(0).toUpperCase() + active.slice(1).replace(/-/g, " ");

  return (
    <div className="app-shell">
      <Sidebar active={active} onNavigate={setActive} />

      <main className="workspace">
        <header className="topbar">
          <div>
            <h1>{title}</h1>
            <p>Administrative panel</p>
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
        {active === "roles" && <Roles />}
      </main>
    </div>
  );
}
