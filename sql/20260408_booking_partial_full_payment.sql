-- Booking partial/full payment migration
-- Adds booking payment mode tracking and upfront payment amount, then expands payment enum values.

SET SQL_SAFE_UPDATES = 0;

ALTER TABLE bookings
    ADD COLUMN payment_mode ENUM('partial','full') NOT NULL DEFAULT 'partial' AFTER deposit_amount,
    ADD COLUMN upfront_payment_amount DECIMAL(12,2) NOT NULL DEFAULT 0.00 AFTER payment_mode;

UPDATE bookings b
JOIN (
    SELECT booking_id
    FROM bookings
    WHERE payment_mode IS NULL OR upfront_payment_amount = 0.00
) x ON x.booking_id = b.booking_id
SET b.payment_mode = 'partial',
    b.upfront_payment_amount = b.deposit_amount;

ALTER TABLE payments
    MODIFY COLUMN payment_type ENUM('deposit','balance','addon','refund','upfront') NOT NULL,
    MODIFY COLUMN payment_purpose ENUM('booking_deposit','booking_balance','booking_full_payment','booking_addon_or_package','subscription_monthly','subscription_annual','subscription_trial','subscription_renewal','refund_booking','refund_subscription','other') NOT NULL DEFAULT 'booking_deposit';

SET SQL_SAFE_UPDATES = 1;