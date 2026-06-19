import { useEffect, useState } from "react";
import { CheckCircle2, Edit3, Plus, PowerOff } from "lucide-react";
import { ConfirmDialog, DetailGrid, EntityModal, PageHeader, SearchInput } from "../components/Crud";
import { Field } from "../components/Field";
import { PaginationControls } from "../components/PaginationControls";
import { api } from "../api";
import { useAppContext } from "../context/AppContext";
import { useI18n } from "../i18n";
import type { Pagination, UserSummary, RoleSummary } from "../types";
import { addRequired, addEmail } from "../utils/validation";

const initialDraft = {
  fullName: "",
  email: "",
  phone: "",
  temporaryPassword: "ChangeMe!12345",
  roleId: "",
};

export function Users() {
  const { currentTenant, currentStore } = useAppContext();
  const { t } = useI18n();
  const tenantId = currentTenant?.id ?? "";
  const storeId = currentStore?.id ?? "";
  const [users, setUsers] = useState<UserSummary[]>([]);
  const [roles, setRoles] = useState<RoleSummary[]>([]);
  const [draft, setDraft] = useState(initialDraft);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [pagination, setPagination] = useState<Pagination | null>(null);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [message, setMessage] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [userToDisable, setUserToDisable] = useState<UserSummary | null>(null);
  const [userToActivate, setUserToActivate] = useState<UserSummary | null>(null);
  const [selectedUser, setSelectedUser] = useState<UserSummary | null>(null);
  const [detailUser, setDetailUser] = useState<UserSummary | null>(null);
  const [changePassword, setChangePassword] = useState(false);
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");

  useEffect(() => {
    void loadUsers(1);
  }, [tenantId]);

  async function loadUsers(nextPage = page) {
    setIsLoading(true);
    setMessage(null);
    try {
      const data = await api.users.list(tenantId.trim() || undefined, { page: nextPage, search });
      setUsers(data.users ?? []);
      setPagination(data.pagination ?? null);
      setPage(data.pagination?.page ?? nextPage);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("users.loadFailed"));
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
      setMessage(err instanceof Error ? err.message : t("users.rolesLoadFailed"));
    } finally {
      setIsLoading(false);
    }
  }

  async function saveUser() {
    const validationErrors = validateUserDraft(draft, roles, selectedUser !== null);
    setErrors(validationErrors);
    setMessage(null);
    if (Object.keys(validationErrors).length > 0) return;
    if (selectedUser && changePassword) {
      if (newPassword.length < 10) {
        setMessage(t("users.passwordTooShort"));
        return;
      }
      if (newPassword !== confirmPassword) {
        setMessage(t("users.passwordsDoNotMatch"));
        return;
      }
    }

    const role = roles.find((r) => r.id === draft.roleId);
    if (role?.scope !== "System" && !tenantId) {
      setMessage(t("users.selectTenantForUser"));
      return;
    }
    if (role?.scope === "Store" && !storeId) {
      setMessage(t("users.selectStoreForUser"));
      return;
    }

    try {
      const payload = {
        fullName: draft.fullName.trim(),
        email: draft.email.trim(),
        phone: draft.phone.trim() || null,
        roleId: draft.roleId,
        tenantId: role?.scope === "System" ? null : tenantId,
        storeId: role?.scope === "Store" ? storeId : null,
      };

      if (selectedUser) {
        await api.users.update(selectedUser.id, payload);
        if (changePassword) {
          await api.users.changePassword(selectedUser.id, {
            newPassword,
            confirmPassword,
            tenantId: tenantId || null,
            requirePasswordChangeOnNextLogin: false,
          });
        }
        setMessage(t("users.updated"));
      } else {
        await api.users.create({
          ...payload,
          temporaryPassword: draft.temporaryPassword,
        });
        setMessage(t("users.created"));
      }
      setDraft(initialDraft);
      setChangePassword(false);
      setNewPassword("");
      setConfirmPassword("");
      setSelectedUser(null);
      setIsModalOpen(false);
      await loadUsers(selectedUser ? page : 1);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("users.saveFailed"));
    }
  }

  async function disableUser(userId: string) {
    setMessage(null);
    try {
      await api.users.disable(userId);
      setMessage(t("users.disabled"));
      setUserToDisable(null);
      await loadUsers(page);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("users.disableFailed"));
    }
  }

  async function activateUser(userId: string) {
    setMessage(null);
    try {
      await api.users.activate(userId);
      setMessage(t("users.activated"));
      setUserToActivate(null);
      await loadUsers(page);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("users.activateFailed"));
    }
  }

  const selectedRole = roles.find((r) => r.id === draft.roleId);

  function newUser() {
    setSelectedUser(null);
    setDraft(initialDraft);
    setErrors({});
    setChangePassword(false);
    setNewPassword("");
    setConfirmPassword("");
    setMessage(null);
    setIsModalOpen(true);
    if (roles.length === 0) {
      void loadRoles();
    }
  }

  function editUser(user: UserSummary) {
    const role = user.roles[0];
    setSelectedUser(user);
    setDraft({
      fullName: user.fullName,
      email: user.email,
      phone: user.phone ?? "",
      temporaryPassword: "",
      roleId: role?.roleId ?? "",
    });
    setErrors({});
    setChangePassword(false);
    setNewPassword("");
    setConfirmPassword("");
    setMessage(null);
    setDetailUser(null);
    setIsModalOpen(true);
    if (roles.length === 0) {
      void loadRoles();
    }
  }

  return (
    <section className="users-panel">
      <div className="panel-section">
        <PageHeader
          title={t("users.title")}
          description={t("users.description")}
          actions={<button className="primary" type="button" onClick={newUser}><Plus size={16} />{t("common.addNew")}</button>}
        />
        <SearchInput value={search} onChange={setSearch} onSearch={() => loadUsers(1)} isLoading={isLoading} />
        {message && <div className={/created|updated|disabled|activated/i.test(message) ? "notice success" : "notice error"}>{message}</div>}
      </div>

      <div className="table-shell">
        <table>
          <thead>
            <tr>
              <th>{t("common.name")}</th>
              <th>{t("users.email")}</th>
              <th>{t("users.roles")}</th>
              <th>{t("common.status")}</th>
              <th>{t("users.lastLogin")}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {users.length === 0 ? (
              <tr><td colSpan={6}>{t("users.noUsersLoaded")}</td></tr>
            ) : (
              users.map((user) => (
                <tr key={user.id}>
                  <td><button className="row-link" onClick={() => setDetailUser(user)}>{user.fullName}</button></td>
                  <td>{user.email}</td>
                  <td>{user.roles.map((r) => r.roleName || r.scope).join(", ") || t("users.noRoles")}</td>
                  <td>{user.isActive ? t("common.active") : t("common.disabled")}</td>
                  <td>{user.lastLoginAt ? new Date(user.lastLoginAt).toLocaleString() : t("common.never")}</td>
                  <td>
                    <div className="row-actions">
                      <button className="secondary" onClick={() => editUser(user)}><Edit3 size={16} />{t("common.edit")}</button>
                      {user.isActive ? (
                        <button className="secondary" onClick={() => setUserToDisable(user)}><PowerOff size={16} />{t("common.disable")}</button>
                      ) : (
                        <button className="secondary" onClick={() => setUserToActivate(user)}><CheckCircle2 size={16} />{t("common.activate")}</button>
                      )}
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
        <PaginationControls pagination={pagination} onPageChange={loadUsers} />
      </div>

      {isModalOpen && (
        <EntityModal
          title={selectedUser ? t("users.editUser") : t("users.newUser")}
          onClose={() => setIsModalOpen(false)}
          footer={(
            <>
              <button className="secondary" type="button" onClick={() => setIsModalOpen(false)}>{t("common.cancel")}</button>
              <button className="primary" type="button" onClick={saveUser}>{selectedUser ? t("users.updateUser") : t("users.createUser")}</button>
            </>
          )}
        >
          <div className="form-grid">
            <Field label={t("users.fullName")} value={draft.fullName} error={errors["user.fullName"]} required onChange={(v) => setDraft({ ...draft, fullName: v })} />
            <Field label={t("users.email")} type="email" value={draft.email} error={errors["user.email"]} required onChange={(v) => setDraft({ ...draft, email: v })} />
            <Field label={t("users.phone")} value={draft.phone} onChange={(v) => setDraft({ ...draft, phone: v })} />
            {!selectedUser && (
              <Field label={t("users.temporaryPassword")} type="password" value={draft.temporaryPassword} error={errors["user.temporaryPassword"]} required onChange={(v) => setDraft({ ...draft, temporaryPassword: v })} />
            )}
            <label className="field">
              <span>{t("users.role")} *</span>
              <select value={draft.roleId} aria-invalid={errors["user.roleId"] ? "true" : "false"} onChange={(e) => setDraft({ ...draft, roleId: e.target.value })}>
                <option value="">{t("users.selectRole")}</option>
                {roles.map((role) => (
                  <option key={role.id} value={role.id}>{role.name} &middot; {role.scope}</option>
                ))}
              </select>
              {errors["user.roleId"] && <small className="field-error">{errors["user.roleId"]}</small>}
            </label>
            {selectedRole?.scope !== "System" && (
              <div className="context-pill">
                {currentTenant ? `Tenant: ${currentTenant.name}` : t("users.selectTenantContext")}
              </div>
            )}
            {selectedRole?.scope === "Store" && (
              <div className={currentStore ? "context-pill" : "context-pill error"}>
                {currentStore ? `Store: ${currentStore.name}` : t("users.selectStoreContext")}
              </div>
            )}
          </div>
          {selectedUser && (
            <div className="panel-section" style={{ marginTop: "1rem" }}>
              <label className="toggle">
                <input type="checkbox" checked={changePassword} onChange={(event) => setChangePassword(event.target.checked)} />
                <span>{t("users.changePassword")}</span>
              </label>
              {changePassword && (
                <div className="form-grid" style={{ marginTop: "1rem" }}>
                  <Field label={t("users.newPassword")} type="password" value={newPassword} required onChange={setNewPassword} />
                  <Field label={t("users.confirmPassword")} type="password" value={confirmPassword} required onChange={setConfirmPassword} />
                </div>
              )}
            </div>
          )}
        </EntityModal>
      )}

      {userToDisable && (
        <ConfirmDialog
          title={t("users.disableTitle")}
          message={t("users.disableMessage").replace("{name}", userToDisable.fullName)}
          confirmLabel={t("common.disable")}
          onCancel={() => setUserToDisable(null)}
          onConfirm={() => disableUser(userToDisable.id)}
        />
      )}

      {userToActivate && (
        <ConfirmDialog
          title={t("users.activateTitle")}
          message={t("users.activateMessage").replace("{name}", userToActivate.fullName)}
          confirmLabel={t("common.activate")}
          onCancel={() => setUserToActivate(null)}
          onConfirm={() => activateUser(userToActivate.id)}
        />
      )}

      {detailUser && (
        <EntityModal
          title={t("users.details")}
          onClose={() => setDetailUser(null)}
          footer={(
            <>
              <button className="secondary" type="button" onClick={() => setDetailUser(null)}>{t("common.close")}</button>
              <button className="primary" type="button" onClick={() => editUser(detailUser)}>{t("common.edit")}</button>
            </>
          )}
        >
          <DetailGrid
            items={[
              { label: t("common.name"), value: detailUser.fullName },
              { label: t("users.email"), value: detailUser.email },
              { label: t("users.phone"), value: detailUser.phone ?? "-" },
              { label: t("users.roles"), value: detailUser.roles.map((role) => role.roleName || role.scope).join(", ") || t("users.noRoles") },
              { label: t("common.status"), value: detailUser.isActive ? t("common.active") : t("common.disabled") },
              { label: t("users.lastLogin"), value: detailUser.lastLoginAt ? new Date(detailUser.lastLoginAt).toLocaleString() : t("common.never") },
            ]}
          />
        </EntityModal>
      )}
    </section>
  );
}

function validateUserDraft(draft: typeof initialDraft, roles: RoleSummary[], isEditing = false) {
  const errors: Record<string, string> = {};
  addRequired(errors, "user.fullName", draft.fullName, "Full name is required.");
  addRequired(errors, "user.email", draft.email, "Email is required.");
  addEmail(errors, "user.email", draft.email);
  if (!isEditing) {
    addRequired(errors, "user.temporaryPassword", draft.temporaryPassword, "Temporary password is required.");
    if (draft.temporaryPassword.trim() && draft.temporaryPassword.length < 10) errors["user.temporaryPassword"] = "Use at least 10 characters.";
  }
  addRequired(errors, "user.roleId", draft.roleId, "Role is required.");
  return errors;
}
