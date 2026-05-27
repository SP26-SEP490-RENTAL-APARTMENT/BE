ALTER TABLE booking_check_times
    ADD COLUMN landlord_pending_credit_amount DECIMAL(12,2) NOT NULL DEFAULT 0.00;

ALTER TABLE booking_check_times
    ADD COLUMN landlord_funds_released_at DATETIME NULL;

CREATE INDEX idx_booking_check_times_landlord_funds_released_at
    ON booking_check_times (landlord_funds_released_at);
