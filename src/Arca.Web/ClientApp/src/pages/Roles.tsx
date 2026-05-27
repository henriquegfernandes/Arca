import { useState } from "react";
import { Field } from "../components/Field";
import { Toggle } from "../components/Toggle";
import { PaginationControls } from "../components/PaginationControls";
import { api } from "../api";
import type { Pagination, RoleDetails, Permission } from "../types";
import { addRequired, addUuid } from "../utils/validation";

const initialDraft = { tenantId: "", name: "", description: "", scope: "Store", permissions: [] as string[] };

export function Roles() {
  const [tenantId, setTenantId] = useState("");
  const [roles, setRoles] = useState<RoleDetails[]>([]);
  const [permissions, setPermissions] = useState<Permission[]>([]);
  const [draft, setDraft] = useState(initialDraft);
  const [selectedRoleId, setSelectedRoleId] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [pagination, setPagination] = useState<Pagination | null>(null);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [message, setMessage] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  async function loadRoles(nextPage = page) {
    setIsLoading(true);
    setMessage(null);
    try {
      const data = await api.roles.list(tenantId.trim() || undefined, { page: nextPage, search });
      setRoles(data.roles ?? []);
      setPagination(data.pagination ?? null);
      setPage(data.pagination?.page ?? nextPage);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : "Could not load roles.");
    } finally {
      setIsLoading(false);
    }
  }

  async function loadPermissions() {
    setIsLoading(true);
    setMessage(null);
    try {
      const data = await api.roles.permissions();
      setPermissions(data.permissions ?? []);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : "Could not load permissions.");
    } finally {
      setIsLoading(false);
    }
  }

  function selectRole(role: RoleDetails) {
    setSelectedRoleId(role.id);
    setDraft({ tenantId: role.tenantId ?? "", name: role.name, description: role.description ?? "", scope: role.scope, permissions: role.permissions });
    setErrors({});
    setMessage(null);
  }

  function newRole() {
    setSelectedRoleId(null);
    setDraft({ ...initialDraft, tenantId });
    setErrors({});
    setMessage(null);
  }

  function togglePermission(permission: string, checked: boolean) {
    const next = checked ? [...draft.permissions, permission] : draft.permissions.filter((p) => p !== permission);
    setDraft({ ...draft, permissions: [...new Set(next)] });
  }

  async function createRole() {
    const validationErrors = validateRoleDraft(draft, false);
    setErrors(validationErrors);
    setMessage(null);
    if (Object.keys(validationErrors).length > 0) return;

    try {
      await api.roles.create({
        tenantId: draft.scope === "System" ? null : draft.tenantId.trim(),
        name: draft.name.trim(),
        description: draft.description.trim() || null,
        scope: draft.scope,
        permissions: draft.permissions,
      });
      setMessage("Role created.");
      setDraft({ ...initialDraft, tenantId });
      await loadRoles(1);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : "Could not create role.");
    }
  }

  async function updatePermissions() {
    if (!selectedRoleId) { setMessage("Select a role first."); return; }
    const validationErrors = validateRoleDraft(draft, true);
    setErrors(validationErrors);
    setMessage(null);
    if (Object.keys(validationErrors).length > 0) return;

    try {
      await api.roles.updatePermissions(selectedRoleId, draft.permissions);
      setMessage("Permissions updated.");
      await loadRoles(page);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : "Could not update permissions.");
    }
  }

  async function disableRole(roleId: string) {
    setMessage(null);
    try {
      await api.roles.disable(roleId);
      setMessage("Role disabled.");
      await loadRoles(page);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : "Could not disable role.");
    }
  }

  const permissionsByModule = permissions.reduce<Record<string, Permission[]>>((groups, p) => {
    (groups[p.module] = groups[p.module] ?? []).push(p);
    return groups;
  }, {});

  return (
    <section className="roles-panel">
      <div className="panel-section">
        <div className="section-heading">
          <div>
            <h2>Roles</h2>
            <p>Role scopes and permission sets.</p>
          </div>
          <div className="row-actions">
            <button className="secondary" onClick={loadPermissions} disabled={isLoading}>Load Permissions</button>
            <button className="secondary" onClick={() => loadRoles(1)} disabled={isLoading}>Load Roles</button>
          </div>
        </div>
        <div className="form-grid compact">
          <Field label="TenantId" value={tenantId} onChange={(v) => { setTenantId(v); setDraft({ ...draft, tenantId: v }); }} />
          <Field label="Search" value={search} onChange={setSearch} />
        </div>
        {message && <div className={message.endsWith(".") ? "notice success" : "notice error"}>{message}</div>}
      </div>

      <div className="panel-section">
        <div className="section-heading">
          <div>
            <h2>{selectedRoleId ? "Role Permissions" : "New Role"}</h2>
          </div>
          <button className="secondary" onClick={newRole}>New</button>
        </div>

        <div className="form-grid">
          <Field label="Name" value={draft.name} error={errors["role.name"]} required onChange={(v) => setDraft({ ...draft, name: v })} />
          <label className="field">
            <span>Scope *</span>
            <select value={draft.scope} disabled={!!selectedRoleId} aria-invalid={errors["role.scope"] ? "true" : "false"} onChange={(e) => setDraft({ ...draft, scope: e.target.value })}>
              <option value="System">System</option>
              <option value="Tenant">Tenant</option>
              <option value="Store">Store</option>
            </select>
            {errors["role.scope"] && <small className="field-error">{errors["role.scope"]}</small>}
          </label>
          {draft.scope !== "System" && <Field label="TenantId" value={draft.tenantId} error={errors["role.tenantId"]} required onChange={(v) => setDraft({ ...draft, tenantId: v })} />}
          <Field label="Description" value={draft.description} onChange={(v) => setDraft({ ...draft, description: v })} />
        </div>

        <div className="permission-groups">
          {Object.keys(permissionsByModule).length === 0 ? (
            <div className="empty-state">Load permissions first.</div>
          ) : (
            Object.entries(permissionsByModule).map(([module, modulePermissions]) => (
              <div key={module} className="permission-group">
                <strong>{module}</strong>
                {modulePermissions.map((perm) => (
                  <Toggle key={perm.name} label={perm.name} checked={draft.permissions.includes(perm.name)} onChange={(c) => togglePermission(perm.name, c)} />
                ))}
              </div>
            ))
          )}
        </div>
        {errors["role.permissions"] && <div className="field-error">{errors["role.permissions"]}</div>}

        <div className="actions left">
          {selectedRoleId ? (
            <button className="primary" onClick={updatePermissions}>Update Permissions</button>
          ) : (
            <button className="primary" onClick={createRole}>Create Role</button>
          )}
        </div>
      </div>

      <div className="table-shell">
        <table>
          <thead>
            <tr>
              <th>Name</th>
              <th>Scope</th>
              <th>Permissions</th>
              <th>Status</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {roles.length === 0 ? (
              <tr><td colSpan={5}>No roles loaded.</td></tr>
            ) : (
              roles.map((role) => (
                <tr key={role.id}>
                  <td>{role.name}</td>
                  <td>{role.scope}</td>
                  <td>{role.permissions.length}</td>
                  <td>{role.isActive ? "Active" : "Disabled"}</td>
                  <td>
                    <div className="row-actions">
                      <button className="secondary" onClick={() => selectRole(role)}>Edit</button>
                      {!role.isSystemRole && role.isActive && <button className="secondary" onClick={() => disableRole(role.id)}>Disable</button>}
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
        <PaginationControls pagination={pagination} onPageChange={loadRoles} />
      </div>
    </section>
  );
}

function validateRoleDraft(draft: typeof initialDraft, permissionsOnly: boolean) {
  const errors: Record<string, string> = {};
  if (!permissionsOnly) {
    addRequired(errors, "role.name", draft.name, "Role name is required.");
    addRequired(errors, "role.scope", draft.scope, "Scope is required.");
    if (!["System", "Tenant", "Store"].includes(draft.scope)) errors["role.scope"] = "Scope is invalid.";
    if (draft.scope !== "System") {
      addRequired(errors, "role.tenantId", draft.tenantId, "TenantId is required for this scope.");
      addUuid(errors, "role.tenantId", draft.tenantId);
    }
  }
  if (draft.permissions.length === 0) errors["role.permissions"] = "Choose at least one permission.";
  return errors;
}
