-- Add missing columns to payments table for fee splitting and settlement tracking
ALTER TABLE payments
    ADD COLUMN landlord_id CHAR(36) NULL,
    ADD COLUMN settlement_status VARCHAR(20) NOT NULL DEFAULT 'pending',
    MODIFY COLUMN landlord_amount DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    MODIFY COLUMN platform_fee DECIMAL(12,2) NOT NULL DEFAULT 0.00;

