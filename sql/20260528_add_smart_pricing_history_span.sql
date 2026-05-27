-- Migration: add start_date and end_date to smart_pricing_history
-- Run this on your MySQL database after deploying the backend change.

START TRANSACTION;

ALTER TABLE `smart_pricing_history`
  ADD COLUMN `start_date` DATE NULL AFTER `date`,
  ADD COLUMN `end_date` DATE NULL AFTER `start_date`;

-- Backfill existing rows from the legacy single-day date column.
UPDATE `smart_pricing_history`
SET `start_date` = `date`,
    `end_date` = `date`
WHERE `start_date` IS NULL OR `end_date` IS NULL;

ALTER TABLE `smart_pricing_history`
  MODIFY COLUMN `start_date` DATE NOT NULL,
  MODIFY COLUMN `end_date` DATE NOT NULL;

CREATE INDEX `idx_apartment_date_range`
  ON `smart_pricing_history` (`apartment_id`, `start_date`, `end_date`);

COMMIT;

-- Notes:
-- 1) This keeps the legacy `date` column in place for compatibility.
-- 2) New code treats `start_date`/`end_date` as the real suggestion span.