-- Add check_time_state_events table for auditing check-time state transitions

CREATE TABLE IF NOT EXISTS `check_time_state_events` (
  `event_id` CHAR(36) NOT NULL,
  `booking_id` CHAR(36) NOT NULL,
  `check_time_id` CHAR(36) NULL,
  `event_type` VARCHAR(100) NOT NULL,
  `event_data` TEXT NULL,
  `created_by` CHAR(36) NULL,
  `created_at` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`event_id`),
  KEY `idx_event_booking` (`booking_id`),
  KEY `idx_event_check_time` (`check_time_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- Down (manual): DROP TABLE IF EXISTS `check_time_state_events`;
