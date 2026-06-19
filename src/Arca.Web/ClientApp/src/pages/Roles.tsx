import { useEffect, useState } from "react";
import { CheckCircle2, Edit3, Plus, PowerOff, Trash2 } from "lucide-react";
import { ConfirmDialog, EntityModal, PageHeader, SearchInput } from "../components/Crud";
import { Field } from "../components/Field";
import { Toggle } from "../components/Toggle";
import { PaginationControls } from "../components/PaginationControls";
import { api } from "../api";
import { useAppContext } from "../context/AppContext";
import { useI18n } from "../i18n";
import type { Pagination, RoleDetails, Permission } from "../types";
import { addRequired } from "../utils/validation";

const initialDraft = { name: "", description: "", scope: "Store", permissions: [] as string[] };

export function Roles() {
  const { currentTenant } = useAppContext();
  const { t } = useI18n();
  const tenantId = currentTenant?.id ?? "";
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
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [roleToDisable, setRoleToDisable] = useState<RoleDetails | null>(null);
  const [roleToActivate, setRoleToActivate] = useState<RoleDetails | null>(null);
  const [roleToDelete, setRoleToDelete] = useState<RoleDetails | null>(null);

  useEffect(() => {
    void loadRoles(1);
  }, [tenantId]);

  async function loadRoles(nextPage = page) {
    setIsLoading(true);
    setMessage(null);
    try {
      const data = await api.roles.list(tenantId.trim() || undefined, { page: nextPage, search });
      setRoles(data.roles ?? []);
      setPagination(data.pagination ?? null);
      setPage(data.pagination?.page ?? nextPage);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("roles.loadFailed"));
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
      setMessage(err instanceof Error ? err.message : t("roles.permissionsLoadFailed"));
    } finally {
      setIsLoading(false);
    }
  }

  function selectRole(role: RoleDetails) {
    setSelectedRoleId(role.id);
    setDraft({ name: role.name, description: role.description ?? "", scope: role.scope, permissions: role.permissions });
    setErrors({});
    setMessage(null);
    setIsModalOpen(true);
    if (permissions.length === 0) {
      void loadPermissions();
    }
  }

  function newRole() {
    setSelectedRoleId(null);
    setDraft(initialDraft);
    setErrors({});
    setMessage(null);
    setIsModalOpen(true);
    if (permissions.length === 0) {
      void loadPermissions();
    }
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
    if (draft.scope !== "System" && !tenantId) {
      setMessage(t("roles.selectTenantBeforeCreate"));
      return;
    }

    try {
      await api.roles.create({
        tenantId: draft.scope === "System" ? null : tenantId,
        name: draft.name.trim(),
        description: draft.description.trim() || null,
        scope: draft.scope,
        permissions: draft.permissions,
      });
      setMessage(t("roles.created"));
      setDraft(initialDraft);
      setIsModalOpen(false);
      await loadRoles(1);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("roles.createFailed"));
    }
  }

  async function updatePermissions() {
    if (!selectedRoleId) { setMessage(t("roles.selectRoleFirst")); return; }
    const validationErrors = validateRoleDraft(draft, true);
    setErrors(validationErrors);
    setMessage(null);
    if (Object.keys(validationErrors).length > 0) return;

    try {
      await api.roles.updatePermissions(selectedRoleId, draft.permissions);
      setMessage(t("roles.permissionsUpdated"));
      setIsModalOpen(false);
      await loadRoles(page);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("roles.updateFailed"));
    }
  }

  async function disableRole(roleId: string) {
    setMessage(null);
    try {
      await api.roles.disable(roleId);
      setMessage(t("roles.disabled"));
      setRoleToDisable(null);
      await loadRoles(page);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("roles.disableFailed"));
    }
  }

  async function deleteRole(roleId: string) {
    setMessage(null);
    try {
      await api.roles.delete(roleId);
      setMessage(t("roles.deleted"));
      setRoleToDelete(null);
      await loadRoles(page);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("roles.deleteFailed"));
    }
  }

  async function activateRole(roleId: string) {
    setMessage(null);
    try {
      await api.roles.activate(roleId);
      setMessage(t("roles.activated"));
      setRoleToActivate(null);
      await loadRoles(page);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("roles.activateFailed"));
    }
  }

  const permissionsByModule = permissions.reduce<Record<string, Permission[]>>((groups, p) => {
    (groups[p.module] = groups[p.module] ?? []).push(p);
    return groups;
  }, {});

  return (
    <section className="roles-panel">
      <div className="panel-section">
        <PageHeader
          title={t("roles.title")}
          description={t("roles.description")}
          actions={<button className="primary" type="button" onClick={newRole}><Plus size={16} />{t("common.addNew")}</button>}
        />
        <SearchInput value={search} onChange={setSearch} onSearch={() => loadRoles(1)} isLoading={isLoading} />
        {message && (
          <div className={/created|updated|disabled|activated|deleted/i.test(message) ? "notice success" : "notice error"}>
            {message}
          </div>
        )}
      </div>

      <div className="table-shell">
        <table>
          <thead>
            <tr>
              <th>{t("common.name")}</th>
              <th>{t("roles.scope")}</th>
              <th>{t("roles.permissions")}</th>
              <th>{t("common.status")}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {roles.length === 0 ? (
              <tr><td colSpan={5}>{t("roles.noRolesLoaded")}</td></tr>
            ) : (
              roles.map((role) => (
                <tr key={role.id}>
                  <td>{role.name}</td>
                  <td>{role.scope}</td>
                  <td>{role.permissions.length}</td>
                  <td>{role.isActive ? t("common.active") : t("common.disabled")}</td>
                  <td>
                    <div className="row-actions">
                      <button className="secondary" onClick={() => selectRole(role)}><Edit3 size={16} />{t("common.edit")}</button>
                      {!role.isSystemRole && (role.isActive ? (
                        <button className="secondary" onClick={() => setRoleToDisable(role)}><PowerOff size={16} />{t("common.disable")}</button>
                      ) : (
                        <button className="secondary" onClick={() => setRoleToActivate(role)}><CheckCircle2 size={16} />{t("common.activate")}</button>
                      ))}
                      {!role.isSystemRole && role.scope !== "System" && (
                        <button className="secondary danger" onClick={() => setRoleToDelete(role)}><Trash2 size={16} />{t("common.delete")}</button>
                      )}
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
        <PaginationControls pagination={pagination} onPageChange={loadRoles} />
      </div>

      {isModalOpen && (
        <EntityModal
          title={selectedRoleId ? t("roles.rolePermissions") : t("roles.newRole")}
          onClose={() => setIsModalOpen(false)}
          footer={(
            <>
              <button className="secondary" type="button" onClick={() => setIsModalOpen(false)}>{t("common.cancel")}</button>
              {selectedRoleId ? (
                <button className="primary" type="button" onClick={updatePermissions}>{t("roles.updatePermissions")}</button>
              ) : (
                <button className="primary" type="button" onClick={createRole}>{t("roles.createRole")}</button>
              )}
            </>
          )}
        >
          <div className="form-grid">
            <Field label={t("common.name")} value={draft.name} error={errors["role.name"]} required onChange={(v) => setDraft({ ...draft, name: v })} />
            <label className="field">
              <span>{t("roles.scope")} *</span>
              <select value={draft.scope} disabled={!!selectedRoleId} aria-invalid={errors["role.scope"] ? "true" : "false"} onChange={(e) => setDraft({ ...draft, scope: e.target.value })}>
                <option value="System">System</option>
                <option value="Tenant">Tenant</option>
                <option value="Store">Store</option>
              </select>
              {errors["role.scope"] && <small className="field-error">{errors["role.scope"]}</small>}
            </label>
            {draft.scope !== "System" && (
              <div className={currentTenant ? "context-pill" : "context-pill error"}>
                {currentTenant ? `Tenant: ${currentTenant.name}` : t("roles.selectTenant")}
              </div>
            )}
            <Field label={t("common.description")} value={draft.description} onChange={(v) => setDraft({ ...draft, description: v })} />
          </div>

          <div className="permission-groups">
            {Object.keys(permissionsByModule).length === 0 ? (
              <div className="empty-state">{t("roles.loadingPermissions")}</div>
            ) : (
              Object.entries(permissionsByModule).map(([module, modulePermissions]) => (
                <div key={module} className="permission-group">
                  <strong>{module}</strong>
                  <div className="permission-options-grid">
                    {modulePermissions.map((perm) => (
                      <Toggle key={perm.name} label={perm.name} checked={draft.permissions.includes(perm.name)} onChange={(c) => togglePermission(perm.name, c)} />
                    ))}
                  </div>
                </div>
              ))
            )}
          </div>
          {errors["role.permissions"] && <div className="field-error">{errors["role.permissions"]}</div>}
        </EntityModal>
      )}

      {roleToDisable && (
        <ConfirmDialog
          title={t("roles.disableTitle")}
          message={t("roles.disableMessage").replace("{name}", roleToDisable.name)}
          confirmLabel={t("common.disable")}
          onCancel={() => setRoleToDisable(null)}
          onConfirm={() => disableRole(roleToDisable.id)}
        />
      )}

      {roleToActivate && (
        <ConfirmDialog
          title={t("roles.activateTitle")}
          message={t("roles.activateMessage").replace("{name}", roleToActivate.name)}
          confirmLabel={t("common.activate")}
          onCancel={() => setRoleToActivate(null)}
          onConfirm={() => activateRole(roleToActivate.id)}
        />
      )}

      {roleToDelete && (
        <ConfirmDialog
          title={t("roles.deleteTitle")}
          message={t("roles.deleteMessage").replace("{name}", roleToDelete.name)}
          confirmLabel={t("common.delete")}
          onCancel={() => setRoleToDelete(null)}
          onConfirm={() => deleteRole(roleToDelete.id)}
        />
      )}
    </section>
  );
}

function validateRoleDraft(draft: typeof initialDraft, permissionsOnly: boolean) {
  const errors: Record<string, string> = {};
  if (!permissionsOnly) {
    addRequired(errors, "role.name", draft.name, "Role name is required.");
    addRequired(errors, "role.scope", draft.scope, "Scope is required.");
    if (!["System", "Tenant", "Store"].includes(draft.scope)) errors["role.scope"] = "Scope is invalid.";
  }
  if (draft.permissions.length === 0) errors["role.permissions"] = "Choose at least one permission.";
  return errors;
}
