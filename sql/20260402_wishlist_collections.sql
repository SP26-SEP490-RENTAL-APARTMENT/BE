-- Wishlist collections migration
-- Adds tenant-owned collections and allows one apartment to be in multiple collections.

CREATE TABLE IF NOT EXISTS wishlist_collections (
    collection_id CHAR(36) PRIMARY KEY,
    tenant_id CHAR(36) NOT NULL,
    name VARCHAR(100) NOT NULL,
    description VARCHAR(500) NULL,
    is_default TINYINT(1) NOT NULL DEFAULT 0,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    CONSTRAINT wishlist_collections_ibfk_1 FOREIGN KEY (tenant_id)
        REFERENCES tenants(tenant_id) ON DELETE CASCADE,
    CONSTRAINT uk_wishlist_collections_tenant_name UNIQUE (tenant_id, name),
    INDEX idx_wishlist_collections_tenant_id (tenant_id)
);

ALTER TABLE tenant_wishlists
    ADD COLUMN collection_id CHAR(36) NULL AFTER apartment_id;

ALTER TABLE tenant_wishlists
    ADD INDEX idx_collection_id (collection_id);

-- Bootstrap default collection per tenant.
INSERT INTO wishlist_collections (collection_id, tenant_id, name, description, is_default)
SELECT UUID(), t.tenant_id, 'General', 'Default wishlist collection', 1
FROM tenants t
LEFT JOIN wishlist_collections c
    ON c.tenant_id = t.tenant_id AND c.is_default = 1
WHERE c.collection_id IS NULL;

-- Backfill existing wishlist rows into tenant default collection.
UPDATE tenant_wishlists w
JOIN wishlist_collections c
    ON c.tenant_id = w.tenant_id AND c.is_default = 1
SET w.collection_id = c.collection_id
WHERE w.collection_id IS NULL;

ALTER TABLE tenant_wishlists
    MODIFY COLUMN collection_id CHAR(36) NOT NULL;

-- Replace old uniqueness with collection-aware uniqueness.
ALTER TABLE tenant_wishlists
    DROP INDEX uk_tenant_apartment,
    ADD CONSTRAINT uk_collection_apartment UNIQUE (collection_id, apartment_id);

ALTER TABLE tenant_wishlists
    ADD CONSTRAINT tenant_wishlists_ibfk_3 FOREIGN KEY (collection_id)
        REFERENCES wishlist_collections(collection_id) ON DELETE CASCADE;
