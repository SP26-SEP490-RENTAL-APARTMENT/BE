-- Migration: add late check-in flag to booking_check_times
-- Run this on your MySQL database after deploying the backend changes.

START TRANSACTION;

ALTER TABLE `booking_check_times`
  ADD COLUMN `is_late_check_in` tinyint(1) NULL AFTER `actual_check_in`;

COMMIT;

-- This flag is set when an actual check-in is recorded after the scheduled check-in time.