CREATE TABLE IF NOT EXISTS inventory_balance (
    id UUID PRIMARY KEY,
    stock_location_id UUID NOT NULL REFERENCES stock_location(id),
    product_variant_id UUID NOT NULL REFERENCES product_variant(id),
    quantity INT NOT NULL,
    reserved_quantity INT NOT NULL,
    minimum_stock INT NOT NULL,
    updated_at TIMESTAMP NULL,
    CONSTRAINT uq_inventory_balance_location_variant UNIQUE (stock_location_id, product_variant_id),
    CONSTRAINT ck_inventory_balance_quantity CHECK (quantity >= 0),
    CONSTRAINT ck_inventory_balance_reserved_quantity CHECK (reserved_quantity >= 0),
    CONSTRAINT ck_inventory_balance_minimum_stock CHECK (minimum_stock >= 0)
);

CREATE TABLE IF NOT EXISTS inventory_batch (
    id UUID PRIMARY KEY,
    stock_location_id UUID NOT NULL REFERENCES stock_location(id),
    product_variant_id UUID NOT NULL REFERENCES product_variant(id),
    batch_number VARCHAR(120) NULL,
    expiration_date TIMESTAMP NULL,
    quantity INT NOT NULL,
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP NULL,
    CONSTRAINT ck_inventory_batch_quantity CHECK (quantity >= 0)
);

CREATE TABLE IF NOT EXISTS stock_movement (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL REFERENCES tenant(id),
    store_id UUID NOT NULL REFERENCES store(id),
    stock_location_id UUID NOT NULL REFERENCES stock_location(id),
    product_variant_id UUID NOT NULL REFERENCES product_variant(id),
    type VARCHAR(40) NOT NULL,
    quantity INT NOT NULL,
    unit_cost NUMERIC(18,2) NULL,
    reason VARCHAR(255) NULL,
    notes TEXT NULL,
    user_id UUID NULL REFERENCES app_user(id),
    created_at TIMESTAMP NOT NULL,
    CONSTRAINT ck_stock_movement_type CHECK (type IN ('Purchase', 'Sale', 'Return', 'Adjustment', 'TransferIn', 'TransferOut', 'Loss', 'Production', 'Consumption')),
    CONSTRAINT ck_stock_movement_quantity CHECK (quantity <> 0)
);

CREATE INDEX IF NOT EXISTS idx_inventory_balance_location_id ON inventory_balance(stock_location_id);
CREATE INDEX IF NOT EXISTS idx_inventory_balance_variant_id ON inventory_balance(product_variant_id);
CREATE INDEX IF NOT EXISTS idx_inventory_batch_location_id ON inventory_batch(stock_location_id);
CREATE INDEX IF NOT EXISTS idx_inventory_batch_variant_id ON inventory_batch(product_variant_id);
CREATE INDEX IF NOT EXISTS idx_stock_movement_tenant_id ON stock_movement(tenant_id);
CREATE INDEX IF NOT EXISTS idx_stock_movement_store_id ON stock_movement(store_id);
CREATE INDEX IF NOT EXISTS idx_stock_movement_location_id ON stock_movement(stock_location_id);
CREATE INDEX IF NOT EXISTS idx_stock_movement_variant_id ON stock_movement(product_variant_id);
CREATE INDEX IF NOT EXISTS idx_stock_movement_created_at ON stock_movement(created_at);
