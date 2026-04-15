ALTER TABLE booking_check_times
    ADD COLUMN fee_settlement_status VARCHAR(30) NOT NULL DEFAULT 'none' AFTER early_check_in_fee,
    ADD COLUMN fee_due_at DATETIME NULL AFTER fee_settlement_status,
    ADD COLUMN fee_settled_at DATETIME NULL AFTER fee_due_at,
    ADD COLUMN fee_settlement_notes TEXT NULL AFTER fee_settled_at;
