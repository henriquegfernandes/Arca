CREATE EXTENSION IF NOT EXISTS "pgcrypto";

CREATE TABLE IF NOT EXISTS tenant (
    id UUID PRIMARY KEY,
    name VARCHAR(200) NOT NULL,
    legal_name VARCHAR(200) NULL,
    document VARCHAR(40) NULL,
    slug VARCHAR(120) NOT NULL UNIQUE,
    is_active BOOLEAN NOT NULL,
    setup_status VARCHAR(30) NOT NULL,
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP NULL
);

CREATE TABLE IF NOT EXISTS tenant_settings (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL REFERENCES tenant(id),
    currency VARCHAR(10) NOT NULL,
    time_zone VARCHAR(100) NOT NULL,
    default_language VARCHAR(10) NOT NULL,
    allow_multiple_stores BOOLEAN NOT NULL,
    allow_batch_control BOOLEAN NOT NULL,
    allow_expiration_control BOOLEAN NOT NULL,
    allow_store_specific_pricing BOOLEAN NOT NULL,
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP NULL
);

CREATE TABLE IF NOT EXISTS store (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL REFERENCES tenant(id),
    name VARCHAR(200) NOT NULL,
    code VARCHAR(40) NOT NULL,
    document VARCHAR(40) NULL,
    phone VARCHAR(30) NULL,
    email VARCHAR(200) NULL,
    address_line VARCHAR(255) NULL,
    city VARCHAR(120) NULL,
    state VARCHAR(120) NULL,
    zip_code VARCHAR(20) NULL,
    type VARCHAR(50) NOT NULL,
    is_active BOOLEAN NOT NULL,
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP NULL,
    CONSTRAINT uq_store_tenant_code UNIQUE (tenant_id, code)
);

CREATE INDEX IF NOT EXISTS idx_store_tenant_id ON store(tenant_id);
CREATE INDEX IF NOT EXISTS idx_tenant_slug ON tenant(slug);
