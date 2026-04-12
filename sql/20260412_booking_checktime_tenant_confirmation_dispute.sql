
-- Tenant check-time confirmation/dispute workflow columns

SET SQL_SAFE_UPDATES = 0;

ALTER TABLE booking_check_times
    ADD COLUMN tenant_response_status VARCHAR(50) NULL,
    ADD COLUMN tenant_responded_by CHAR(36) NULL,
    ADD COLUMN tenant_responded_at DATETIME NULL,
    ADD COLUMN tenant_dispute_reason VARCHAR(300) NULL,
    ADD COLUMN tenant_dispute_notes TEXT NULL,
    ADD COLUMN dispute_resolution_status VARCHAR(80) NULL,
    ADD COLUMN dispute_resolved_by CHAR(36) NULL,
    ADD COLUMN dispute_resolved_at DATETIME NULL,
    ADD COLUMN dispute_resolution_notes TEXT NULL;

-- Backfill existing rows so UI/API can treat old records consistently.
UPDATE booking_check_times
SET tenant_response_status = COALESCE(tenant_response_status, 'pending')
WHERE actual_check_in IS NOT NULL OR actual_check_out IS NOT NULL;
SET SQL_SAFE_UPDATES = 1;
