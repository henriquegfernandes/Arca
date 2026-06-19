CREATE TABLE IF NOT EXISTS product_image (
    id UUID PRIMARY KEY,
    product_id UUID NOT NULL REFERENCES product(id),
    product_variant_id UUID NULL REFERENCES product_variant(id),
    file_name VARCHAR(255) NOT NULL,
    original_file_name VARCHAR(255) NOT NULL,
    content_type VARCHAR(120) NOT NULL,
    storage_provider VARCHAR(40) NOT NULL,
    storage_path TEXT NOT NULL,
    public_url TEXT NULL,
    alt_text VARCHAR(255) NULL,
    sort_order INT NOT NULL,
    is_main BOOLEAN NOT NULL,
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP NULL,
    CONSTRAINT ck_product_image_storage_provider CHECK (storage_provider IN ('Local', 'S3'))
);

CREATE INDEX IF NOT EXISTS idx_product_image_product_id ON product_image(product_id);
CREATE INDEX IF NOT EXISTS idx_product_image_variant_id ON product_image(product_variant_id);
CREATE INDEX IF NOT EXISTS idx_product_image_sort ON product_image(product_id, product_variant_id, sort_order);
