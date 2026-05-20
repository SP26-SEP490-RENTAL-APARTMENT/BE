-- Migration: add offline payment proof and confirmation columns to payments
-- Run this on your MySQL database (e.g., via `mysql` CLI or your DB tool)

START TRANSACTION;

ALTER TABLE `payments`
  ADD COLUMN `proof_url` varchar(500) NULL AFTER `transaction_id`,
  ADD COLUMN `confirmed_by` char(36) NULL AFTER `proof_url`,
  ADD COLUMN `confirmed_at` timestamp NULL AFTER `confirmed_by`,
  ADD COLUMN `notes` text NULL AFTER `confirmed_at`;

COMMIT;

-- Notes:
-- 1) `confirmed_by` is expected to reference a user id (char(36)).
-- 2) If you use strict foreign keys, consider adding a FK constraint to `users(user_id)`.
-- 3) After applying this migration, update any DTOs/serializers that expose `Payment` if needed.
