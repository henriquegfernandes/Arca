import { useState } from "react";
import { Field } from "../components/Field";
import { PaginationControls } from "../components/PaginationControls";
import { api } from "../api";
import type { Pagination, UserSummary, RoleSummary } from "../types";
import { addRequired, addUuid, addEmail } from "../utils/validation";

const initialDraft = {
  fullName: "",
  email: "",
  phone: "",
  temporaryPassword: "ChangeMe!12345",
  roleId: "",
  tenantId: "",
  storeId: "",
};

export function Users() {
  const [tenantId, setTenantId] = useState("");
  const [users, setUsers] = useState<UserSummary[]>([]);
  const [roles, setRoles] = useState<RoleSummary[]>([]);
  const [draft, setDraft] = useState(initialDraft);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [pagination, setPagination] = useState<Pagination | null>(null);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [message, setMessage] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  async function loadUsers(nextPage = page) {
    setIsLoading(true);
    setMessage(null);
    try {
      const data = await api.users.list(tenantId.trim() || undefined, { page: nextPage, search });
      setUsers(data.users ?? []);
      setPagination(data.pagination ?? null);
      setPage(data.pagination?.page ?? nextPage);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : "Could not load users.");
    } finally {
      setIsLoading(false);
    }
  }

  async function loadRoles() {
    setIsLoading(true);
    setMessage(null);
    try {
      const data = await api.users.roles(tenantId.trim() || undefined, { pageSize: 100 });
      setRoles(data.roles ?? []);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : "Could not load roles.");
    } finally {
      setIsLoading(false);
    }
  }

  async function createUser() {
    const validationErrors = validateUserDraft(draft, roles);
    setErrors(validationErrors);
    setMessage(null);
    if (Object.keys(validationErrors).length > 0) return;

    const role = roles.find((r) => r.id === draft.roleId);
    try {
      await api.users.create({
        fullName: draft.fullName.trim(),
        email: draft.email.trim(),
        phone: draft.phone.trim() || null,
        temporaryPassword: draft.temporaryPassword,
        roleId: draft.roleId,
        tenantId: role?.scope === "System" ? null : draft.tenantId.trim(),
        storeId: role?.scope === "Store" ? draft.storeId.trim() : null,
      });
      setMessage("User created.");
      setDraft({ ...initialDraft, tenantId: draft.tenantId });
      await loadUsers(1);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : "Could not create user.");
    }
  }

  async function disableUser(userId: string) {
    setMessage(null);
    try {
      await api.users.disable(userId);
      setMessage("User disabled.");
      await loadUsers(page);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : "Could not disable user.");
    }
  }

  const selectedRole = roles.find((r) => r.id === draft.roleId);

  return (
    <section className="users-panel">
      <div className="panel-section">
        <div className="section-heading">
          <div>
            <h2>Users</h2>
            <p>Users, scoped roles and tenant/store access.</p>
          </div>
          <div className="row-actions">
            <button className="secondary" onClick={loadRoles} disabled={isLoading}>Load Roles</button>
            <button className="secondary" onClick={() => loadUsers(1)} disabled={isLoading}>Load Users</button>
          </div>
        </div>
        <div className="form-grid compact">
          <Field label="TenantId" value={tenantId} onChange={(v) => { setTenantId(v); setDraft({ ...draft, tenantId: v }); }} />
          <Field label="Search" value={search} onChange={setSearch} />
        </div>
        {message && <div className={message.endsWith("created.") || message.endsWith("disabled.") ? "notice success" : "notice error"}>{message}</div>}
      </div>

      <div className="panel-section">
        <div className="section-heading">
          <div>
            <h2>New User</h2>
          </div>
        </div>
        <div className="form-grid">
          <Field label="Full Name" value={draft.fullName} error={errors["user.fullName"]} required onChange={(v) => setDraft({ ...draft, fullName: v })} />
          <Field label="Email" type="email" value={draft.email} error={errors["user.email"]} required onChange={(v) => setDraft({ ...draft, email: v })} />
          <Field label="Phone" value={draft.phone} onChange={(v) => setDraft({ ...draft, phone: v })} />
          <Field label="Temporary Password" type="password" value={draft.temporaryPassword} error={errors["user.temporaryPassword"]} required onChange={(v) => setDraft({ ...draft, temporaryPassword: v })} />
          <label className="field">
            <span>Role *</span>
            <select value={draft.roleId} aria-invalid={errors["user.roleId"] ? "true" : "false"} onChange={(e) => setDraft({ ...draft, roleId: e.target.value })}>
              <option value="">Select a role</option>
              {roles.map((role) => (
                <option key={role.id} value={role.id}>{role.name} &middot; {role.scope}</option>
              ))}
            </select>
            {errors["user.roleId"] && <small className="field-error">{errors["user.roleId"]}</small>}
          </label>
          <Field label="TenantId" value={draft.tenantId} error={errors["user.tenantId"]} onChange={(v) => setDraft({ ...draft, tenantId: v })} />
          {selectedRole?.scope === "Store" && (
            <Field label="StoreId" value={draft.storeId} error={errors["user.storeId"]} onChange={(v) => setDraft({ ...draft, storeId: v })} />
          )}
        </div>
        <div className="actions left">
          <button className="primary" onClick={createUser}>Create User</button>
        </div>
      </div>

      <div className="table-shell">
        <table>
          <thead>
            <tr>
              <th>Name</th>
              <th>Email</th>
              <th>Roles</th>
              <th>Status</th>
              <th>Last Login</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {users.length === 0 ? (
              <tr><td colSpan={6}>No users loaded.</td></tr>
            ) : (
              users.map((user) => (
                <tr key={user.id}>
                  <td>{user.fullName}</td>
                  <td>{user.email}</td>
                  <td>{user.roles.map((r) => r.roleName || r.scope).join(", ") || "No roles"}</td>
                  <td>{user.isActive ? "Active" : "Disabled"}</td>
                  <td>{user.lastLoginAt ? new Date(user.lastLoginAt).toLocaleString() : "Never"}</td>
                  <td>{user.isActive && <button className="secondary" onClick={() => disableUser(user.id)}>Disable</button>}</td>
                </tr>
              ))
            )}
          </tbody>
        </table>
        <PaginationControls pagination={pagination} onPageChange={loadUsers} />
      </div>
    </section>
  );
}

function validateUserDraft(draft: typeof initialDraft, roles: RoleSummary[]) {
  const errors: Record<string, string> = {};
  const role = roles.find((r) => r.id === draft.roleId);
  addRequired(errors, "user.fullName", draft.fullName, "Full name is required.");
  addRequired(errors, "user.email", draft.email, "Email is required.");
  addEmail(errors, "user.email", draft.email);
  addRequired(errors, "user.temporaryPassword", draft.temporaryPassword, "Temporary password is required.");
  if (draft.temporaryPassword.trim() && draft.temporaryPassword.length < 10) errors["user.temporaryPassword"] = "Use at least 10 characters.";
  addRequired(errors, "user.roleId", draft.roleId, "Role is required.");
  if (role?.scope === "Tenant" || role?.scope === "Store") {
    addRequired(errors, "user.tenantId", draft.tenantId, "TenantId is required for this role.");
    addUuid(errors, "user.tenantId", draft.tenantId);
  }
  if (role?.scope === "Store") {
    addRequired(errors, "user.storeId", draft.storeId, "StoreId is required for this role.");
    addUuid(errors, "user.storeId", draft.storeId);
  }
  return errors;
}
