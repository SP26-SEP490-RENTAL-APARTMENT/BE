-- Migration: add bank profile fields to users
-- Run this on your MySQL database after deploying the backend changes.

START TRANSACTION;

ALTER TABLE `users`
  ADD COLUMN `bank_account_holder_name` varchar(150) NULL AFTER `national_id_card_number`,
  ADD COLUMN `bank_account_number` varchar(50) NULL AFTER `bank_account_holder_name`,
  ADD COLUMN `bank_name` varchar(150) NULL AFTER `bank_account_number`,
  ADD COLUMN `bank_bin` varchar(20) NULL AFTER `bank_name`;

COMMIT;

-- The webhook stores the payer bank details on the tenant's user profile.
-- Users can also update these fields manually through `PUT /api/user/me/bank-profile`.