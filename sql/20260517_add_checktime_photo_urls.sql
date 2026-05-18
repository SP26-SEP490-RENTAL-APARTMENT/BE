-- Add photo evidence URL columns to booking_check_times
-- Run this against the Apartment database (MySQL / MariaDB)

ALTER TABLE `booking_check_times`
  ADD COLUMN `check_in_photo_url` VARCHAR(500) NULL AFTER `notes`,
  ADD COLUMN `check_out_photo_url` VARCHAR(500) NULL AFTER `check_in_photo_url`,
  ADD COLUMN `claim_opened_at` DATETIME NULL AFTER `check_out_photo_url`,
  ADD COLUMN `claim_expires_at` DATETIME NULL AFTER `claim_opened_at`,
  ADD COLUMN `claim_locked_at` DATETIME NULL AFTER `claim_expires_at`,
  ADD COLUMN `claim_status` VARCHAR(30) NULL AFTER `claim_locked_at`,
  ADD COLUMN `no_show_status` VARCHAR(30) NULL AFTER `claim_status`,
  ADD COLUMN `no_show_marked_by` CHAR(36) NULL AFTER `no_show_status`,
  ADD COLUMN `no_show_marked_at` DATETIME NULL AFTER `no_show_marked_by`,
  ADD COLUMN `missing_checkout_status` VARCHAR(40) NULL AFTER `no_show_marked_at`,
  ADD COLUMN `auto_closed_at` DATETIME NULL AFTER `missing_checkout_status`;

-- Optional: add index if you expect queries filtering by claim status/expiry
-- CREATE INDEX `idx_claim_expires_at` ON `booking_check_times` (`claim_expires_at`);

-- Down migration (to rollback):
-- ALTER TABLE `booking_check_times` DROP COLUMN `auto_closed_at`, DROP COLUMN `missing_checkout_status`, DROP COLUMN `no_show_marked_at`, DROP COLUMN `no_show_marked_by`, DROP COLUMN `no_show_status`, DROP COLUMN `claim_status`, DROP COLUMN `claim_locked_at`, DROP COLUMN `claim_expires_at`, DROP COLUMN `claim_opened_at`, DROP COLUMN `check_out_photo_url`, DROP COLUMN `check_in_photo_url`;
