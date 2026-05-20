-- Migration: add amount_paid and remaining_amount to bookings
-- Run this on your MySQL database (e.g., via `mysql` CLI or your DB tool)

START TRANSACTION;

ALTER TABLE `bookings`
  ADD COLUMN `amount_paid` DECIMAL(12,2) NOT NULL DEFAULT '0.00' AFTER `total_price`,
  ADD COLUMN `remaining_amount` DECIMAL(12,2) NOT NULL DEFAULT '0.00' AFTER `amount_paid`;

-- Backfill amount_paid from successful non-refund payments
UPDATE `bookings` b
SET b.`amount_paid` = (
  SELECT IFNULL(SUM(p.`amount`), 0)
  FROM `payments` p
  WHERE p.`related_entity_type` = 'booking'
    AND p.`related_entity_id` = b.`booking_id`
    AND p.`status` = 'success'
    AND p.`payment_type` <> 'refund'
);

-- Backfill remaining_amount (ensure non-negative and round to 2 decimals)
UPDATE `bookings` b
SET b.`remaining_amount` = GREATEST(0, ROUND(b.`total_price` - b.`amount_paid`, 2));

COMMIT;

-- Notes:
-- 1) If your DB uses a different schema or table/column names, adjust accordingly.
-- 2) If the app writes these fields from code, future updates will keep them in sync; consider running this migration during a maintenance window.
-- 3) If you prefer an EF Core migration instead, I can generate that instead.
