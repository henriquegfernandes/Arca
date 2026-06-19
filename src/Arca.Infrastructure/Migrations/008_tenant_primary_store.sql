ALTER TABLE tenant
    ADD COLUMN IF NOT EXISTS primary_store_id UUID NULL REFERENCES store(id);

CREATE INDEX IF NOT EXISTS idx_tenant_primary_store_id ON tenant(primary_store_id);
