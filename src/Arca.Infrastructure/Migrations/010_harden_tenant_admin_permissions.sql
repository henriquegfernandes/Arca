DELETE FROM role_permission rp
USING role r, permission p
WHERE rp.role_id = r.id
  AND rp.permission_id = p.id
  AND r.normalized_name = 'TENANTADMIN'
  AND r.scope = 'Tenant'
  AND p.name IN ('roles.view', 'roles.manage', 'audit.view');
