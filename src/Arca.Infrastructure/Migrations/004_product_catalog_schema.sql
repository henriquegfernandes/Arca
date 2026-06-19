CREATE TABLE IF NOT EXISTS product (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL REFERENCES tenant(id),
    category_id UUID NULL REFERENCES category(id),
    product_type_id UUID NULL REFERENCES product_type(id),
    name VARCHAR(200) NOT NULL,
    slug VARCHAR(200) NOT NULL,
    description TEXT NULL,
    base_sku VARCHAR(120) NOT NULL,
    barcode VARCHAR(120) NULL,
    brand VARCHAR(120) NULL,
    status VARCHAR(40) NOT NULL,
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP NULL,
    CONSTRAINT uq_product_tenant_base_sku UNIQUE (tenant_id, base_sku),
    CONSTRAINT uq_product_tenant_slug UNIQUE (tenant_id, slug),
    CONSTRAINT ck_product_status CHECK (status IN ('Draft', 'Active', 'Inactive'))
);

CREATE TABLE IF NOT EXISTS product_attribute_assignment (
    id UUID PRIMARY KEY,
    product_id UUID NOT NULL REFERENCES product(id),
    product_attribute_id UUID NOT NULL REFERENCES product_attribute(id),
    product_attribute_value_id UUID NULL REFERENCES product_attribute_value(id),
    text_value TEXT NULL,
    number_value INT NULL,
    decimal_value NUMERIC(18,2) NULL,
    boolean_value BOOLEAN NULL,
    date_value TIMESTAMP NULL
);

CREATE TABLE IF NOT EXISTS product_variant_option (
    id UUID PRIMARY KEY,
    product_id UUID NOT NULL REFERENCES product(id),
    product_attribute_id UUID NOT NULL REFERENCES product_attribute(id),
    product_attribute_value_id UUID NOT NULL REFERENCES product_attribute_value(id),
    CONSTRAINT uq_product_variant_option UNIQUE (product_id, product_attribute_id, product_attribute_value_id)
);

CREATE TABLE IF NOT EXISTS product_variant (
    id UUID PRIMARY KEY,
    product_id UUID NOT NULL REFERENCES product(id),
    sku VARCHAR(160) NOT NULL UNIQUE,
    barcode VARCHAR(120) NULL,
    name VARCHAR(240) NOT NULL,
    default_sale_price NUMERIC(18,2) NOT NULL,
    default_cost_price NUMERIC(18,2) NULL,
    status VARCHAR(40) NOT NULL,
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP NULL,
    CONSTRAINT ck_product_variant_status CHECK (status IN ('Draft', 'Active', 'Inactive'))
);

CREATE TABLE IF NOT EXISTS product_variant_attribute_value (
    id UUID PRIMARY KEY,
    product_variant_id UUID NOT NULL REFERENCES product_variant(id),
    product_attribute_id UUID NOT NULL REFERENCES product_attribute(id),
    product_attribute_value_id UUID NOT NULL REFERENCES product_attribute_value(id),
    CONSTRAINT uq_product_variant_attribute_value UNIQUE (product_variant_id, product_attribute_id)
);

CREATE TABLE IF NOT EXISTS store_product_variant (
    id UUID PRIMARY KEY,
    store_id UUID NOT NULL REFERENCES store(id),
    product_variant_id UUID NOT NULL REFERENCES product_variant(id),
    sale_price NUMERIC(18,2) NULL,
    cost_price NUMERIC(18,2) NULL,
    is_available BOOLEAN NOT NULL,
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP NULL,
    CONSTRAINT uq_store_product_variant UNIQUE (store_id, product_variant_id)
);

CREATE INDEX IF NOT EXISTS idx_product_tenant_id ON product(tenant_id);
CREATE INDEX IF NOT EXISTS idx_product_category_id ON product(category_id);
CREATE INDEX IF NOT EXISTS idx_product_product_type_id ON product(product_type_id);
CREATE INDEX IF NOT EXISTS idx_product_slug ON product(slug);
CREATE INDEX IF NOT EXISTS idx_product_base_sku ON product(base_sku);
CREATE INDEX IF NOT EXISTS idx_product_attribute_assignment_product_id ON product_attribute_assignment(product_id);
CREATE INDEX IF NOT EXISTS idx_product_variant_option_product_id ON product_variant_option(product_id);
CREATE INDEX IF NOT EXISTS idx_product_variant_product_id ON product_variant(product_id);
CREATE INDEX IF NOT EXISTS idx_product_variant_sku ON product_variant(sku);
CREATE INDEX IF NOT EXISTS idx_product_variant_attribute_value_variant_id ON product_variant_attribute_value(product_variant_id);
CREATE INDEX IF NOT EXISTS idx_store_product_variant_store_id ON store_product_variant(store_id);
CREATE INDEX IF NOT EXISTS idx_store_product_variant_variant_id ON store_product_variant(product_variant_id);
