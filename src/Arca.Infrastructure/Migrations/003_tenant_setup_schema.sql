ALTER TABLE tenant
    ADD COLUMN IF NOT EXISTS contact_email VARCHAR(200) NULL,
    ADD COLUMN IF NOT EXISTS phone VARCHAR(30) NULL,
    ADD COLUMN IF NOT EXISTS main_segment VARCHAR(80) NULL;

CREATE TABLE IF NOT EXISTS stock_location (
    id UUID PRIMARY KEY,
    store_id UUID NOT NULL REFERENCES store(id),
    name VARCHAR(120) NOT NULL,
    type VARCHAR(50) NOT NULL,
    is_active BOOLEAN NOT NULL,
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP NULL
);

CREATE TABLE IF NOT EXISTS category (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL REFERENCES tenant(id),
    parent_category_id UUID NULL REFERENCES category(id),
    name VARCHAR(160) NOT NULL,
    slug VARCHAR(160) NOT NULL,
    description TEXT NULL,
    sort_order INT NOT NULL,
    is_active BOOLEAN NOT NULL,
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP NULL,
    CONSTRAINT uq_category_tenant_slug UNIQUE (tenant_id, slug)
);

CREATE TABLE IF NOT EXISTS product_type (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL REFERENCES tenant(id),
    name VARCHAR(160) NOT NULL,
    description TEXT NULL,
    is_active BOOLEAN NOT NULL,
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP NULL,
    CONSTRAINT uq_product_type_tenant_name UNIQUE (tenant_id, name)
);

CREATE TABLE IF NOT EXISTS product_attribute (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL REFERENCES tenant(id),
    name VARCHAR(160) NOT NULL,
    code VARCHAR(80) NOT NULL,
    description TEXT NULL,
    attribute_type VARCHAR(40) NOT NULL,
    is_variant_attribute BOOLEAN NOT NULL,
    is_required BOOLEAN NOT NULL,
    sort_order INT NOT NULL,
    is_active BOOLEAN NOT NULL,
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP NULL,
    CONSTRAINT uq_product_attribute_tenant_code UNIQUE (tenant_id, code),
    CONSTRAINT ck_product_attribute_type CHECK (attribute_type IN ('Select', 'MultiSelect', 'Text', 'Number', 'Boolean', 'Date', 'Decimal'))
);

CREATE TABLE IF NOT EXISTS product_attribute_value (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL REFERENCES tenant(id),
    product_attribute_id UUID NOT NULL REFERENCES product_attribute(id),
    name VARCHAR(160) NOT NULL,
    code VARCHAR(80) NOT NULL,
    value VARCHAR(255) NULL,
    hex_code VARCHAR(20) NULL,
    sort_order INT NOT NULL,
    is_active BOOLEAN NOT NULL,
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP NULL,
    CONSTRAINT uq_product_attribute_value_attribute_code UNIQUE (product_attribute_id, code)
);

CREATE TABLE IF NOT EXISTS product_type_attribute (
    id UUID PRIMARY KEY,
    product_type_id UUID NOT NULL REFERENCES product_type(id),
    product_attribute_id UUID NOT NULL REFERENCES product_attribute(id),
    is_required BOOLEAN NOT NULL,
    is_variant_attribute BOOLEAN NOT NULL,
    sort_order INT NOT NULL,
    CONSTRAINT uq_product_type_attribute UNIQUE (product_type_id, product_attribute_id)
);

CREATE TABLE IF NOT EXISTS audit_log (
    id UUID PRIMARY KEY,
    user_id UUID NULL REFERENCES app_user(id),
    tenant_id UUID NULL REFERENCES tenant(id),
    store_id UUID NULL REFERENCES store(id),
    action VARCHAR(120) NOT NULL,
    entity_name VARCHAR(120) NOT NULL,
    entity_id UUID NULL,
    old_value TEXT NULL,
    new_value TEXT NULL,
    ip_address VARCHAR(80) NULL,
    user_agent TEXT NULL,
    created_at TIMESTAMP NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_stock_location_store_id ON stock_location(store_id);
CREATE INDEX IF NOT EXISTS idx_category_tenant_id ON category(tenant_id);
CREATE INDEX IF NOT EXISTS idx_category_parent_category_id ON category(parent_category_id);
CREATE INDEX IF NOT EXISTS idx_product_type_tenant_id ON product_type(tenant_id);
CREATE INDEX IF NOT EXISTS idx_product_attribute_tenant_id ON product_attribute(tenant_id);
CREATE INDEX IF NOT EXISTS idx_product_attribute_value_tenant_id ON product_attribute_value(tenant_id);
CREATE INDEX IF NOT EXISTS idx_product_attribute_value_attribute_id ON product_attribute_value(product_attribute_id);
CREATE INDEX IF NOT EXISTS idx_product_type_attribute_product_type_id ON product_type_attribute(product_type_id);
CREATE INDEX IF NOT EXISTS idx_product_type_attribute_attribute_id ON product_type_attribute(product_attribute_id);
CREATE INDEX IF NOT EXISTS idx_audit_log_tenant_id ON audit_log(tenant_id);
CREATE INDEX IF NOT EXISTS idx_audit_log_user_id ON audit_log(user_id);
