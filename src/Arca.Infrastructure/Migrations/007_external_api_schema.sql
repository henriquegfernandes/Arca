CREATE TABLE IF NOT EXISTS api_client (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL REFERENCES tenant(id),
    store_id UUID NULL REFERENCES store(id),
    name VARCHAR(160) NOT NULL,
    api_key_hash TEXT NOT NULL UNIQUE,
    is_active BOOLEAN NOT NULL,
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP NULL,
    last_used_at TIMESTAMP NULL
);

CREATE TABLE IF NOT EXISTS api_client_permission (
    id UUID PRIMARY KEY,
    api_client_id UUID NOT NULL REFERENCES api_client(id),
    permission VARCHAR(80) NOT NULL,
    CONSTRAINT uq_api_client_permission UNIQUE (api_client_id, permission)
);

CREATE TABLE IF NOT EXISTS external_api_request_log (
    id UUID PRIMARY KEY,
    api_client_id UUID NULL REFERENCES api_client(id),
    tenant_id UUID NULL REFERENCES tenant(id),
    store_id UUID NULL REFERENCES store(id),
    path TEXT NOT NULL,
    method VARCHAR(20) NOT NULL,
    status_code INT NOT NULL,
    ip_address VARCHAR(80) NULL,
    user_agent TEXT NULL,
    created_at TIMESTAMP NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_api_client_tenant_id ON api_client(tenant_id);
CREATE INDEX IF NOT EXISTS idx_api_client_store_id ON api_client(store_id);
CREATE INDEX IF NOT EXISTS idx_api_client_api_key_hash ON api_client(api_key_hash);
CREATE INDEX IF NOT EXISTS idx_api_client_permission_client_id ON api_client_permission(api_client_id);
CREATE INDEX IF NOT EXISTS idx_external_api_request_log_client_id ON external_api_request_log(api_client_id);
CREATE INDEX IF NOT EXISTS idx_external_api_request_log_tenant_id ON external_api_request_log(tenant_id);
CREATE INDEX IF NOT EXISTS idx_external_api_request_log_created_at ON external_api_request_log(created_at);
