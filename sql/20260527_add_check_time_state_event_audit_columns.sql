-- Add dedicated audit columns for check_time_state_events

ALTER TABLE `check_time_state_events`
  ADD COLUMN `trigger_source` VARCHAR(50) NULL AFTER `event_data`,
  ADD COLUMN `trigger_reason` VARCHAR(255) NULL AFTER `trigger_source`,
  ADD COLUMN `correlation_id` VARCHAR(100) NULL AFTER `trigger_reason`;
