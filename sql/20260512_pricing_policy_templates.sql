-- Add pricing_policy enum value and create tables for pricing templates and applications
START TRANSACTION;

-- 1) Extend price_type enum to include 'pricing_policy'
ALTER TABLE `apartment_price_calendar`
  MODIFY COLUMN `price_type` enum('base','weekend','holiday','peak_season','low_season','special_event','manual_override','pricing_policy') NOT NULL DEFAULT 'base';

-- 2) Create template library
CREATE TABLE IF NOT EXISTS `pricing_rule_templates` (
  `template_id` char(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `created_by_admin_id` char(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `name` varchar(200) NOT NULL,
  `code` varchar(100) DEFAULT NULL,
  `description` text,
  `is_active` tinyint(1) NOT NULL DEFAULT 1,
  `created_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`template_id`),
  UNIQUE KEY `uk_code` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- 3) Create template parameter table
CREATE TABLE IF NOT EXISTS `pricing_rule_template_parameters` (
  `parameter_id` char(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `template_id` char(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `parameter_key` varchar(100) NOT NULL,
  `display_name` varchar(200) NOT NULL,
  `default_value` decimal(12,4) NOT NULL,
  `min_value` decimal(12,4) DEFAULT NULL,
  `max_value` decimal(12,4) DEFAULT NULL,
  `is_adjustable` tinyint(1) NOT NULL DEFAULT 1,
  `created_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`parameter_id`),
  UNIQUE KEY `uk_template_parameter` (`template_id`,`parameter_key`),
  KEY `idx_template` (`template_id`),
  CONSTRAINT `pricing_rule_template_parameters_ibfk_1` FOREIGN KEY (`template_id`) REFERENCES `pricing_rule_templates` (`template_id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- 4) Create apartment application table
CREATE TABLE IF NOT EXISTS `apartment_pricing_policy_applications` (
  `application_id` char(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `apartment_id` char(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `template_id` char(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `start_date` date NOT NULL,
  `end_date` date NOT NULL,
  `is_enabled` tinyint(1) NOT NULL DEFAULT 1,
  `overrides_json` json DEFAULT NULL,
  `created_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`application_id`),
  UNIQUE KEY `uk_apartment_policy_range` (`apartment_id`,`template_id`,`start_date`,`end_date`),
  KEY `idx_template` (`template_id`),
  CONSTRAINT `apartment_pricing_policy_applications_ibfk_1` FOREIGN KEY (`apartment_id`) REFERENCES `apartments` (`apartment_id`) ON DELETE CASCADE,
  CONSTRAINT `apartment_pricing_policy_applications_ibfk_2` FOREIGN KEY (`template_id`) REFERENCES `pricing_rule_templates` (`template_id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

COMMIT;

-- Note: Run this script using your DB migration workflow (EF Core migrations or manual SQL runner).