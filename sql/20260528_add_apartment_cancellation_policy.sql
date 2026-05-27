-- Add listing-level cancellation policy code for booking refund decisions
-- Run this against the Apartment database (MySQL / MariaDB)

ALTER TABLE `apartments`
  ADD COLUMN `cancellation_policy_code` VARCHAR(50) NULL AFTER `booking_status`;

-- Suggested values:
-- legacy, non_refundable, flexible, moderate, strict

-- Down migration (to rollback):
-- ALTER TABLE `apartments` DROP COLUMN `cancellation_policy_code`;