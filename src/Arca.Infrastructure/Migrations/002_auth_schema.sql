CREATE TABLE IF NOT EXISTS app_user (
    id UUID PRIMARY KEY,
    full_name VARCHAR(200) NOT NULL,
    email VARCHAR(200) NOT NULL,
    normalized_email VARCHAR(200) NOT NULL UNIQUE,
    phone VARCHAR(30) NULL,
    password_hash TEXT NOT NULL,
    is_active BOOLEAN NOT NULL,
    email_confirmed BOOLEAN NOT NULL,
    last_login_at TIMESTAMP NULL,
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP NULL
);

CREATE TABLE IF NOT EXISTS permission (
    id UUID PRIMARY KEY,
    name VARCHAR(120) NOT NULL UNIQUE,
    description VARCHAR(255) NULL,
    module VARCHAR(80) NOT NULL
);

CREATE TABLE IF NOT EXISTS role (
    id UUID PRIMARY KEY,
    tenant_id UUID NULL REFERENCES tenant(id),
    name VARCHAR(120) NOT NULL,
    normalized_name VARCHAR(120) NOT NULL,
    description VARCHAR(255) NULL,
    scope VARCHAR(30) NOT NULL,
    is_system_role BOOLEAN NOT NULL,
    is_active BOOLEAN NOT NULL,
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP NULL,
    CONSTRAINT ck_role_scope CHECK (scope IN ('System', 'Tenant', 'Store'))
);

CREATE TABLE IF NOT EXISTS user_tenant (
    id UUID PRIMARY KEY,
    user_id UUID NOT NULL REFERENCES app_user(id),
    tenant_id UUID NOT NULL REFERENCES tenant(id),
    is_active BOOLEAN NOT NULL,
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP NULL,
    CONSTRAINT uq_user_tenant UNIQUE (user_id, tenant_id)
);

CREATE TABLE IF NOT EXISTS user_store (
    id UUID PRIMARY KEY,
    user_id UUID NOT NULL REFERENCES app_user(id),
    tenant_id UUID NOT NULL REFERENCES tenant(id),
    store_id UUID NOT NULL REFERENCES store(id),
    is_active BOOLEAN NOT NULL,
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP NULL,
    CONSTRAINT uq_user_store UNIQUE (user_id, store_id)
);

CREATE TABLE IF NOT EXISTS user_role (
    id UUID PRIMARY KEY,
    user_id UUID NOT NULL REFERENCES app_user(id),
    role_id UUID NOT NULL REFERENCES role(id),
    tenant_id UUID NULL REFERENCES tenant(id),
    store_id UUID NULL REFERENCES store(id),
    created_at TIMESTAMP NOT NULL,
    CONSTRAINT ck_user_role_store_requires_tenant CHECK (store_id IS NULL OR tenant_id IS NOT NULL)
);

CREATE TABLE IF NOT EXISTS role_permission (
    role_id UUID NOT NULL REFERENCES role(id),
    permission_id UUID NOT NULL REFERENCES permission(id),
    CONSTRAINT pk_role_permission PRIMARY KEY (role_id, permission_id)
);

CREATE TABLE IF NOT EXISTS login_attempt (
    id UUID PRIMARY KEY,
    email VARCHAR(200) NOT NULL,
    success BOOLEAN NOT NULL,
    failure_reason VARCHAR(255) NULL,
    ip_address VARCHAR(80) NULL,
    user_agent TEXT NULL,
    created_at TIMESTAMP NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_role_system_normalized_name
    ON role(normalized_name)
    WHERE tenant_id IS NULL;

CREATE UNIQUE INDEX IF NOT EXISTS uq_role_tenant_normalized_name
    ON role(tenant_id, normalized_name)
    WHERE tenant_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_app_user_normalized_email ON app_user(normalized_email);
CREATE INDEX IF NOT EXISTS idx_user_tenant_user_id ON user_tenant(user_id);
CREATE INDEX IF NOT EXISTS idx_user_tenant_tenant_id ON user_tenant(tenant_id);
CREATE INDEX IF NOT EXISTS idx_user_store_user_id ON user_store(user_id);
CREATE INDEX IF NOT EXISTS idx_user_store_tenant_id ON user_store(tenant_id);
CREATE INDEX IF NOT EXISTS idx_user_store_store_id ON user_store(store_id);
CREATE INDEX IF NOT EXISTS idx_user_role_user_id ON user_role(user_id);
CREATE INDEX IF NOT EXISTS idx_user_role_tenant_id ON user_role(tenant_id);
CREATE INDEX IF NOT EXISTS idx_user_role_store_id ON user_role(store_id);
CREATE INDEX IF NOT EXISTS idx_permission_name ON permission(name);
CREATE INDEX IF NOT EXISTS idx_login_attempt_email_created_at ON login_attempt(email, created_at);
