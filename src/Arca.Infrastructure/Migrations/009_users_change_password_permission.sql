INSERT INTO permission (id, name, description, module)
VALUES (gen_random_uuid(), 'users.change_password', 'Change user passwords', 'Users')
ON CONFLICT (name) DO NOTHING;

INSERT INTO role_permission (role_id, permission_id)
SELECT r.id, p.id
FROM role r
CROSS JOIN permission p
WHERE p.name = 'users.change_password'
  AND r.name IN ('SuperAdmin', 'TenantAdmin')
ON CONFLICT DO NOTHING;
