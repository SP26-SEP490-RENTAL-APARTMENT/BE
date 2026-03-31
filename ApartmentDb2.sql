drop database `ApartmentDb`;

create database `ApartmentDb`;

use `ApartmentDb`;

CREATE TABLE `admin_actions` (
  `action_id` char(36) PRIMARY KEY NOT NULL DEFAULT (uuid()),
  `admin_id` char(36) NOT NULL,
  `action_type` varchar(50) NOT NULL,
  `target_type` ENUM ('user', 'apartment', 'booking', 'review', 'report') NOT NULL,
  `target_id` char(36) NOT NULL,
  `description` varchar(500),
  `previous_value` text,
  `new_value` text,
  `created_at` timestamp DEFAULT (CURRENT_TIMESTAMP)
);

CREATE TABLE `amenities` (
  `amenity_id` char(36) PRIMARY KEY NOT NULL DEFAULT (uuid()),
  `name_en` varchar(100) NOT NULL,
  `name_vi` varchar(100) NOT NULL
);

CREATE TABLE `apartment_amenities` (
  `apartment_id` char(36) NOT NULL,
  `amenity_id` char(36) NOT NULL,
  PRIMARY KEY (`apartment_id`, `amenity_id`)
);

CREATE TABLE `apartment_media` (
  `media_id` char(36) PRIMARY KEY NOT NULL DEFAULT (uuid()),
  `apartment_id` char(36) NOT NULL,
  `url` varchar(500) NOT NULL,
  `type` ENUM ('photo', 'video') NOT NULL,
  `is_primary` tinyint(1) DEFAULT '0'
);

CREATE TABLE `apartment_price_calendar` (
  `price_id` char(36) PRIMARY KEY NOT NULL DEFAULT (uuid()),
  `apartment_id` char(36) NOT NULL,
  `start_date` date NOT NULL,
  `end_date` date NOT NULL,
  `discount_percentage` decimal(5,2) DEFAULT '0.00',
  `is_discount` tinyint(1) DEFAULT '0',
  `price_type` ENUM ('base', 'weekend', 'holiday', 'peak_season', 'low_season', 'special_event', 'manual_override') DEFAULT 'base',
  `min_nights` int DEFAULT '1',
  `created_at` timestamp DEFAULT (CURRENT_TIMESTAMP),
  `updated_at` timestamp DEFAULT (CURRENT_TIMESTAMP)
);

CREATE TABLE `apartment_availability` (
  `availability_id` char(36) PRIMARY KEY NOT NULL DEFAULT (uuid()),
  `apartment_id` char(36) NOT NULL,
  `start_date` date NOT NULL,
  `end_date` date NOT NULL,
  `reason` varchar(200),
  `created_at` timestamp DEFAULT (CURRENT_TIMESTAMP),
  `updated_at` timestamp DEFAULT (CURRENT_TIMESTAMP)
);

CREATE TABLE `apartments` (
  `apartment_id` char(36) PRIMARY KEY NOT NULL DEFAULT (uuid()),
  `landlord_id` char(36) NOT NULL,
  `title` varchar(200) NOT NULL,
  `description` text,
  `max_occupants` tinyint DEFAULT '1',
  `is_pet_allowed` tinyint(1) DEFAULT '0',
  `address` varchar(255),
  `district` varchar(100),
  `city` varchar(100) DEFAULT 'Hồ Chí Minh',
  `latitude` decimal(10,8),
  `longitude` decimal(11,8),
  `location` point NOT NULL,
  `base_price_per_night` decimal(12,2) NOT NULL,
  `status` ENUM ('draft', 'pending_review', 'posted', 'blocked', 'archived') DEFAULT 'draft',
  `booking_status` ENUM ('available', 'confirmed', 'locked') DEFAULT 'available',
  `created_at` timestamp DEFAULT (CURRENT_TIMESTAMP)
);

CREATE TABLE `bookings` (
  `booking_id` char(36) PRIMARY KEY NOT NULL DEFAULT (uuid()),
  `tenant_id` char(36) NOT NULL,
  `apartment_id` char(36) NOT NULL,
  `check_in_date` date NOT NULL,
  `check_out_date` date NOT NULL,
  `nights` int NOT NULL,
  `noOfAdults` int,
  `noOfInfants` int,
  `noOfPets` int,
  `total_price` decimal(12,2) NOT NULL,
  `package_id` char(36),
  `package_price` decimal(12,2) DEFAULT '0.00',
  `deposit_amount` decimal(12,2) NOT NULL,
  `deposit_paid` tinyint(1) DEFAULT '0',
  `balance_due_date` date NOT NULL,
  `status` ENUM ('pending', 'negotiating', 'confirmed', 'paid', 'completed', 'cancelled', 'disputed') DEFAULT 'pending',
  `created_at` timestamp DEFAULT (CURRENT_TIMESTAMP)
);

CREATE TABLE `holidays_events` (
  `event_id` char(36) PRIMARY KEY NOT NULL DEFAULT (uuid()),
  `event_name` varchar(150) NOT NULL,
  `event_type` ENUM ('national_holiday', 'local_festival', 'international_event', 'school_break', 'major_conference', 'sports_event', 'other') NOT NULL,
  `start_date` date NOT NULL,
  `end_date` date NOT NULL,
  `location_scope` varchar(100) DEFAULT 'Vietnam',
  `description` text,
  `is_recurring` tinyint(1) DEFAULT '0',
  `recurrence_rule` varchar(255),
  `created_at` timestamp DEFAULT (CURRENT_TIMESTAMP),
  `updated_at` timestamp DEFAULT (CURRENT_TIMESTAMP)
);

CREATE TABLE `inspection_photos` (
  `photo_id` char(36) PRIMARY KEY NOT NULL DEFAULT (uuid()),
  `inspection_id` char(36) NOT NULL,
  `file_url` varchar(500) NOT NULL,
  `file_key` varchar(255),
  `description` varchar(200),
  `is_issue` tinyint(1) DEFAULT '0',
  `uploaded_at` timestamp DEFAULT (CURRENT_TIMESTAMP)
);

CREATE TABLE `landlord_subscriptions` (
  `subscription_id` char(36) PRIMARY KEY NOT NULL DEFAULT (uuid()),
  `landlord_id` char(36) NOT NULL,
  `plan_id` char(36) NOT NULL,
  `status` ENUM ('active', 'pending_payment', 'expired', 'cancelled', 'trial') DEFAULT 'pending_payment',
  `start_date` date NOT NULL,
  `end_date` date,
  `renewal_type` ENUM ('monthly', 'annual', 'none') DEFAULT 'monthly',
  `auto_renew` tinyint(1) DEFAULT '1',
  `payment_method` varchar(50),
  `last_payment_id` char(36),
  `created_at` timestamp DEFAULT (CURRENT_TIMESTAMP),
  `updated_at` timestamp DEFAULT (CURRENT_TIMESTAMP)
);

CREATE TABLE `landlords` (
  `landlord_id` char(36) PRIMARY KEY NOT NULL,
  `verified_business` tinyint(1) DEFAULT '0',
  `current_plan_id` char(36),
  `subscription_expires_at` date,
  `identity_verification_status` ENUM ('not_started', 'pending', 'verified', 'rejected') DEFAULT 'not_started',
  `last_verified_at` timestamp,
  `subscription_status` ENUM ('none', 'active', 'expired', 'pending') DEFAULT 'none'
  
);

ALTER TABLE landlords
    ADD COLUMN IF NOT EXISTS momo_wallet_phone VARCHAR(20) NULL,
    ADD COLUMN IF NOT EXISTS payout_receiver_name VARCHAR(150) NULL,
    ADD COLUMN IF NOT EXISTS payout_personal_id VARCHAR(30) NULL,
    ADD COLUMN IF NOT EXISTS payout_bank_account_no VARCHAR(40) NULL,
    ADD COLUMN IF NOT EXISTS payout_bank_card_no VARCHAR(40) NULL,
    ADD COLUMN IF NOT EXISTS payout_bank_code VARCHAR(20) NULL,
    ADD COLUMN IF NOT EXISTS preferred_payout_method VARCHAR(20) NULL;

CREATE TABLE `nearby_attractions` (
  `attraction_id` char(36) PRIMARY KEY NOT NULL DEFAULT (uuid()),
  `name_en` varchar(200) NOT NULL,
  `name_vi` varchar(200) NOT NULL,
  `type` ENUM ('restaurant', 'museum', 'park', 'landmark', 'shopping', 'transport') NOT NULL,
  `location` point NOT NULL,
  `address` varchar(500),
  `city` varchar(100)
);

CREATE TABLE `notifications` (
  `notification_id` char(36) PRIMARY KEY NOT NULL DEFAULT (uuid()),
  `user_id` char(36) NOT NULL,
  `type` ENUM ('booking_created', 'booking_confirmed', 'booking_cancelled', 'booking_upcoming', 'payment_success', 'payment_failed', 'identity_verified', 'identity_rejected', 'listing_approved', 'listing_rejected', 'inspection_scheduled', 'inspection_completed', 'support_ticket_created', 'support_ticket_update', 'support_ticket_resolved', 'review_reminder', 'new_message', 'system_announcement', 'other') NOT NULL,
  `title` varchar(150) NOT NULL,
  `message` text NOT NULL,
  `reference_id` char(36),
  `reference_type` varchar(50),
  `is_read` tinyint(1) DEFAULT '0',
  `read_at` timestamp,
  `created_at` timestamp DEFAULT (CURRENT_TIMESTAMP)
);

CREATE TABLE `package_items` (
  `package_item_id` char(36) PRIMARY KEY NOT NULL DEFAULT (uuid()),
  `item_name` varchar(150) NOT NULL,
  `item_description` text,
  `quantity` decimal(10,2) DEFAULT '1.00',
  `estimated_value` decimal(12,2) DEFAULT '0.00',
  `sort_order` int DEFAULT '0'
);

CREATE TABLE `packages` (
  `package_id` char(36) PRIMARY KEY NOT NULL DEFAULT (uuid()),
  `apartment_id` char(36) NOT NULL,
  `name` varchar(150) NOT NULL,
  `description` text,
  `price` decimal(12,2) NOT NULL,
  `currency` varchar(3) DEFAULT 'VND',
  `is_active` tinyint(1) DEFAULT '1',
  `max_bookings` int,
  `created_at` timestamp DEFAULT (CURRENT_TIMESTAMP)
);

CREATE TABLE `payments` (
  `payment_id` char(36) PRIMARY KEY NOT NULL DEFAULT (uuid()),
  `related_entity_id` char(36),
  `amount` decimal(12,2) NOT NULL,
  `payment_type` ENUM ('deposit', 'balance', 'addon', 'refund') NOT NULL,
  `payment_purpose` ENUM ('booking_deposit', 'booking_balance', 'booking_addon_or_package', 'subscription_monthly', 'subscription_annual', 'subscription_trial', 'subscription_renewal', 'refund_booking', 'refund_subscription', 'other') NOT NULL DEFAULT 'booking_deposit',
  `related_entity_type` ENUM ('booking', 'host_subscription', 'other'),
   `host_id` CHAR(36) NULL,
  `host_amount` DECIMAL(12,2) NOT NULL DEFAULT 0.00,
  `platform_fee` DECIMAL(12,2) NOT NULL DEFAULT 0.00,
  `settlement_status` VARCHAR(20) NOT NULL DEFAULT 'pending',
  `method` varchar(50) NOT NULL,
  `status` ENUM ('pending', 'success', 'failed', 'refunded') DEFAULT 'pending',
  `transaction_id` varchar(100),
  `paid_at` timestamp
);

CREATE TABLE `property_inspections` (
  `inspection_id` char(36) PRIMARY KEY NOT NULL DEFAULT (uuid()),
  `apartment_id` char(36) NOT NULL,
  `inspector_id` char(36) NOT NULL,
  `scheduled_date` date,
  `completed_date` date,
  `status` ENUM ('pending', 'scheduled', 'in_progress', 'passed', 'failed', 're_inspection_needed') DEFAULT 'pending',
  `overall_condition` text,
  `issues_found` text,
  `recommendations` text,
  `approved_for_listing` tinyint(1) DEFAULT '0',
  `approved_at` timestamp,
  `approved_by` char(36)
);

CREATE TABLE `reviews` (
  `review_id` char(36) PRIMARY KEY NOT NULL DEFAULT (uuid()),
  `booking_id` char(36) NOT NULL,
  `reviewer_id` char(36) NOT NULL,
  `reviewed_id` char(36) NOT NULL,
  `apartment_id` char(36),
  `rating` tinyint,
  `comment_en` text,
  `comment_vi` text,
  `created_at` timestamp DEFAULT (CURRENT_TIMESTAMP)
);

CREATE TABLE `rooms` (
  `room_id` char(36) PRIMARY KEY NOT NULL DEFAULT (uuid()),
  `apartment_id` char(36) NOT NULL,
  `title` varchar(150),
  `description` text,
  `room_type` ENUM ('private_single', 'private_double', 'shared_bed', 'studio', 'other') DEFAULT 'private_single',
  `bed_type` ENUM ('single', 'double', 'queen', 'king', 'bunk', 'shared') DEFAULT 'single',
  `size_sqm` decimal(5,2),
  `is_private_bathroom` tinyint(1) DEFAULT '0',
  `created_at` timestamp DEFAULT (CURRENT_TIMESTAMP)
);

CREATE TABLE `smart_pricing_history` (
  `pricing_id` char(36) PRIMARY KEY NOT NULL DEFAULT (uuid()),
  `apartment_id` char(36) NOT NULL,
  `date` date NOT NULL,
  `suggested_price` decimal(12,2) NOT NULL,
  `base_price` decimal(12,2) NOT NULL,
  `multiplier` decimal(5,2) DEFAULT '1.00',
  `reason` varchar(255),
  `occupancy_rate` decimal(5,2),
  `accepted_by_landlord` tinyint(1) DEFAULT '0',
  `created_at` timestamp DEFAULT (CURRENT_TIMESTAMP)
);

CREATE TABLE `subscription_plans` (
  `plan_id` char(36) PRIMARY KEY NOT NULL DEFAULT (uuid()),
  `name` varchar(100) NOT NULL,
  `description` text,
  `price_monthly` decimal(12,2) NOT NULL,
  `price_annual` decimal(12,2),
  `max_apartments` int DEFAULT '1',
  `features` text,
  `is_active` tinyint(1) DEFAULT '1',
  `created_at` timestamp DEFAULT (CURRENT_TIMESTAMP)
);

CREATE TABLE `support_ticket_assignments` (
  `assignment_id` char(36) PRIMARY KEY NOT NULL DEFAULT (uuid()),
  `ticket_id` char(36) NOT NULL,
  `staff_id` char(36) NOT NULL,
  `assigned_at` timestamp DEFAULT (CURRENT_TIMESTAMP),
  `role_in_ticket` ENUM ('primary', 'collaborator') DEFAULT 'primary'
);

CREATE TABLE `support_tickets` (
  `ticket_id` char(36) PRIMARY KEY NOT NULL DEFAULT (uuid()),
  `user_id` char(36) NOT NULL,
  `subject` varchar(200) NOT NULL,
  `description` text NOT NULL,
  `category` ENUM ('booking_issue', 'payment_problem', 'listing_problem', 'account_verification', 'cancellation', 'dispute', 'property_quality', 'other') NOT NULL,
  `priority` ENUM ('low', 'medium', 'high', 'urgent') DEFAULT 'medium',
  `status` ENUM ('open', 'in_progress', 'resolved', 'closed', 'escalated') DEFAULT 'open',
  `created_at` timestamp DEFAULT (CURRENT_TIMESTAMP),
  `updated_at` timestamp DEFAULT (CURRENT_TIMESTAMP),
  `resolved_at` timestamp,
  `resolved_by` char(36),
  `resolution_notes` text
);

CREATE TABLE `temporary_residence_reports` (
  `report_id` char(36) PRIMARY KEY NOT NULL DEFAULT (uuid()),
  `booking_id` char(36) NOT NULL,
  `landlord_id` char(36) NOT NULL,
  `tenant_passport_id` varchar(50) NOT NULL,
  `tenant_nationality` varchar(100) NOT NULL,
  `check_in_date` date NOT NULL,
  `reported_to_police` tinyint(1) DEFAULT '0',
  `report_date` date,
  `report_number` varchar(100)
);

CREATE TABLE `tenants` (
  `tenant_id` char(36) PRIMARY KEY NOT NULL,
  `passport_id` varchar(50),
  `nationality` char(2) COMMENT 'ISO 3166-1 alpha-2 code (e.g. VN, US, KR). Used for temp residence reporting',
  `identity_verification_status` ENUM ('not_started', 'pending', 'verified', 'rejected') DEFAULT 'not_started',
  `last_verified_at` timestamp
);

CREATE TABLE `user_identity_documents` (
  `document_id` char(36) PRIMARY KEY NOT NULL DEFAULT (uuid()),
  `user_id` char(36) NOT NULL,
  `document_type` ENUM ('passport', 'national_id_card', 'drivers_license', 'other_government_id', 'selfie_with_id') NOT NULL,
  `side` ENUM ('front', 'back', 'bio_page', 'other') DEFAULT 'front',
  `file_url` varchar(500) NOT NULL,
  `file_key` varchar(255),
  `mime_type` varchar(100),
  `file_size` bigint,
  `uploaded_at` timestamp DEFAULT (CURRENT_TIMESTAMP),
  `verified_at` timestamp,
  `verification_status` ENUM ('pending', 'verified', 'rejected', 'expired') DEFAULT 'pending',
  `rejection_reason` text,
  `notes` text
);

CREATE TABLE `users` (
  `user_id` char(36) PRIMARY KEY NOT NULL DEFAULT (uuid()),
  `email` varchar(255) NOT NULL,
  `password_hash` varchar(255) NOT NULL,
  `role` ENUM ('tenant', 'landlord', 'admin', 'staff') NOT NULL,
  `full_name` varchar(100),
  `phone` varchar(20),
  `identity_verified` tinyint(1) DEFAULT '0',
  `created_at` timestamp DEFAULT (CURRENT_TIMESTAMP)
);

CREATE TABLE booking_check_times (
    check_time_id       CHAR(36) PRIMARY KEY DEFAULT (UUID()),
    
    booking_id          CHAR(36) NOT NULL,
    
    -- Scheduled (expected) times - set at booking creation
    scheduled_check_in  DATETIME NOT NULL,          -- e.g. 14:00 on check-in date
    scheduled_check_out DATETIME NOT NULL,          -- e.g. 12:00 on check-out date
    
    -- Actual recorded times (set by host or system)
    actual_check_in     DATETIME NULL,              -- when guest really arrived
    actual_check_out    DATETIME NULL,              -- when guest left
    
    -- Late / early flags & fees (calculated or set by host)
    is_late_check_out   TINYINT(1) DEFAULT 0,
    late_check_out_fee  DECIMAL(12,2) DEFAULT 0,    -- extra charge if applicable
    is_early_check_in   TINYINT(1) DEFAULT 0,
    early_check_in_fee  DECIMAL(12,2) DEFAULT 0,
    
    -- Compliance tracking (Vietnam temp residence)
    temp_residence_reported TINYINT(1) DEFAULT 0,
    reported_at         DATETIME NULL,
    report_reference    VARCHAR(100) NULL,           -- police report number / confirmation ID
    
    -- Who recorded the actual time
    recorded_by         CHAR(36) NULL,              -- user_id (host or staff)
    recorded_at         DATETIME NULL,
    
    notes               TEXT NULL,                  -- e.g. "Guest arrived 3 hours late due to flight delay"
    
    created_at          TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at          TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    
    UNIQUE KEY uk_booking (booking_id),
    INDEX idx_booking (booking_id),
    INDEX idx_scheduled_in (scheduled_check_in),
    INDEX idx_actual_in (actual_check_in)
);

CREATE TABLE `package_packages` (
  `id` char(36) PRIMARY KEY NOT NULL DEFAULT (uuid()),
  `package_id` char(36) NOT NULL,
  `package_item_id` char(36) NOT NULL
);

CREATE TABLE `momo_transactions` (
  `id` INT PRIMARY KEY NOT NULL AUTO_INCREMENT,
  `request_id` VARCHAR(100) NOT NULL,
  `partner_code` VARCHAR(50) NOT NULL,
  `amount` BIGINT NOT NULL,
  `type` VARCHAR(50) NOT NULL,
  `request_body` LONGTEXT NOT NULL,
  `response_body` LONGTEXT NOT NULL,
  `status` VARCHAR(50) NOT NULL,
  `result_code` INT NULL,
  `message` VARCHAR(1024) NOT NULL,
  `created_at` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `updated_at` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
  `payment_id` char(36),
  INDEX `ix_momo_transactions_request_id` (`request_id`),
  INDEX `ix_momo_transactions_payment_id` (`payment_id`)
);

CREATE TABLE `report_definitions` (
  `report_id` CHAR(36) NOT NULL,
  `name` VARCHAR(150) NOT NULL,
  `description` TEXT NULL,
  -- standard, custom, scheduled, real_time
  `type` ENUM('standard','custom','scheduled','real_time') NOT NULL DEFAULT 'standard',
  `category` VARCHAR(100) NOT NULL DEFAULT '',
  `is_active` TINYINT(1) NOT NULL DEFAULT 1,
  `created_by` CHAR(36) NULL,
  `created_at` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` TIMESTAMP NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`report_id`),
  KEY `idx_report_definitions_category_type_active` (`category`, `type`, `is_active`)
);

CREATE TABLE `report_query_configs` (
  `report_id` CHAR(36) NOT NULL,
  `dimensions_json` LONGTEXT NULL,
  `filters_json` LONGTEXT NULL,
  `metrics_json` LONGTEXT NULL,
  `time_range_json` LONGTEXT NULL,
  PRIMARY KEY (`report_id`),
  CONSTRAINT `fk_report_query_configs_report_definitions_report_id`
    FOREIGN KEY (`report_id`) REFERENCES `report_definitions`(`report_id`)
    ON DELETE CASCADE
);

CREATE TABLE `scheduled_reports` (
  `scheduled_report_id` CHAR(36) NOT NULL,
  `report_id` CHAR(36) NOT NULL,
  `frequency` VARCHAR(50) NULL,
  `cron_expression` VARCHAR(100) NULL,
  `next_run_at` DATETIME NULL,
  `last_run_at` DATETIME NULL,
  `delivery_channel` VARCHAR(50) NULL,
  `recipients` TEXT NULL,
  PRIMARY KEY (`scheduled_report_id`),
  KEY `idx_scheduled_reports_report_id` (`report_id`),
  KEY `idx_scheduled_reports_next_run_at` (`next_run_at`),
  CONSTRAINT `fk_scheduled_reports_report_definitions_report_id`
    FOREIGN KEY (`report_id`) REFERENCES `report_definitions`(`report_id`)
    ON DELETE CASCADE
);

CREATE TABLE `generated_reports` (
  `generated_report_id` CHAR(36) NOT NULL,
  `report_id` CHAR(36) NOT NULL,
  `requested_by` CHAR(36) NOT NULL,
  `requested_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `status` VARCHAR(50) NOT NULL DEFAULT 'completed',
  `result_summary_json` LONGTEXT NULL,
  `result_json` LONGTEXT NULL,
  `retention_until` DATETIME NULL,
  PRIMARY KEY (`generated_report_id`),
  KEY `idx_generated_reports_report_id` (`report_id`),
  KEY `idx_generated_reports_requested_by_requested_at` (`requested_by`, `requested_at`),
  CONSTRAINT `fk_generated_reports_report_definitions_report_id`
    FOREIGN KEY (`report_id`) REFERENCES `report_definitions`(`report_id`)
    ON DELETE CASCADE
);

CREATE TABLE `landlord_wallets` (
  `landlord_id` CHAR(36) NOT NULL,
  `pending_balance` DECIMAL(12,2) NOT NULL DEFAULT '0.00',
  `available_balance` DECIMAL(12,2) NOT NULL DEFAULT '0.00',
  `updated_at` TIMESTAMP NULL DEFAULT NULL,
  PRIMARY KEY (`landlord_id`),
  CONSTRAINT `landlord_wallets_ibfk_1`
    FOREIGN KEY (`landlord_id`) REFERENCES `landlords` (`landlord_id`)
);
CREATE TABLE `tenant_wishlists` (
  `wishlist_id` CHAR(36) NOT NULL,
  `tenant_id` CHAR(36) NOT NULL,
  `apartment_id` CHAR(36) NOT NULL,
  `is_favorite` TINYINT(1) NOT NULL DEFAULT 0,
  `notes` VARCHAR(500) NULL,
  `created_at` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  
  PRIMARY KEY (`wishlist_id`),
  UNIQUE KEY `uk_tenant_apartment` (`tenant_id`, `apartment_id`),
  KEY `idx_tenant_id` (`tenant_id`),
  
  CONSTRAINT `tenant_wishlists_ibfk_1` 
  FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`tenant_id`) ON DELETE CASCADE ON UPDATE RESTRICT,
  CONSTRAINT `tenant_wishlists_ibfk_2` FOREIGN KEY (`apartment_id`) REFERENCES `apartments` (`apartment_id`) ON DELETE CASCADE ON UPDATE RESTRICT
);

CREATE TABLE IF NOT EXISTS landlord_payouts (
    `payout_id` CHAR(36) NOT NULL,
    `landlord_id` CHAR(36) NOT NULL,
    `amount` BIGINT NOT NULL,
    `fee_amount` BIGINT NOT NULL DEFAULT 0,
    `net_amount` BIGINT NOT NULL,
    `channel` VARCHAR(20) NOT NULL,
    `status` VARCHAR(30) NOT NULL,
    `momo_order_id` VARCHAR(120) NOT NULL,
    `momo_request_id` VARCHAR(120) NOT NULL,
    `momo_trans_id` VARCHAR(120) NULL,
    `result_code` INT NULL,
    `message` VARCHAR(1024) NULL,
    `request_body` LONGTEXT NULL,
    `response_body` LONGTEXT NULL,
    `created_at` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `updated_at` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    `completed_at` TIMESTAMP NULL,
    `failed_at` TIMESTAMP NULL,
    CONSTRAINT `pk_landlord_payouts` PRIMARY KEY (`payout_id`),
    CONSTRAINT `fk_landlord_payouts_landlord` FOREIGN KEY (`landlord_id`) REFERENCES `landlords` (`landlord_id`) ON DELETE CASCADE
);

CREATE INDEX `idx_landlord_payouts_landlord` ON `landlord_payouts` (`landlord_id`);
CREATE INDEX `idx_landlord_payouts_landlord_created` ON `landlord_payouts` (`landlord_id`, `created_at`);
CREATE UNIQUE INDEX `uk_landlord_payouts_momo_request_id` ON `landlord_payouts` (`momo_request_id`);

CREATE INDEX `idx_tenant_id` ON `tenant_wishlists` (`tenant_id`);

CREATE INDEX `idx_admin` ON `admin_actions` (`admin_id`);

CREATE INDEX `idx_target` ON `admin_actions` (`target_type`, `target_id`);

CREATE UNIQUE INDEX `uk_name_en` ON `amenities` (`name_en`);

CREATE UNIQUE INDEX `uk_name_vi` ON `amenities` (`name_vi`);

CREATE INDEX `amenity_id` ON `apartment_amenities` (`amenity_id`);

CREATE INDEX `idx_apartment` ON `apartment_media` (`apartment_id`);

CREATE INDEX `idx_apartment_availability_dates` ON `apartment_availability` (`apartment_id`, `start_date`, `end_date`);

CREATE UNIQUE INDEX `uk_apartment_date_range` ON `apartment_price_calendar` (`apartment_id`, `start_date`, `end_date`);

CREATE INDEX `idx_apartment_dates` ON `apartment_price_calendar` (`apartment_id`, `start_date`, `end_date`);

CREATE INDEX `idx_dates` ON `apartment_price_calendar` (`start_date`, `end_date`);

CREATE INDEX `idx_landlord` ON `apartments` (`landlord_id`);

CREATE INDEX `idx_status` ON `apartments` (`status`);

CREATE INDEX `idx_booking_status` ON `apartments` (`booking_status`);

CREATE INDEX `idx_location` ON `apartments` (`location`);

CREATE INDEX `idx_tenant` ON `bookings` (`tenant_id`);

CREATE INDEX `idx_apartment` ON `bookings` (`apartment_id`);

CREATE INDEX `idx_status` ON `bookings` (`status`);

CREATE INDEX `idx_dates` ON `bookings` (`check_in_date`, `check_out_date`);

CREATE INDEX `idx_package` ON `bookings` (`package_id`);

CREATE UNIQUE INDEX `uk_event_dates` ON `holidays_events` (`event_name`, `start_date`, `end_date`);

CREATE INDEX `idx_dates` ON `holidays_events` (`start_date`, `end_date`);

CREATE INDEX `idx_type_scope` ON `holidays_events` (`event_type`, `location_scope`);

CREATE INDEX `idx_inspection` ON `inspection_photos` (`inspection_id`);

CREATE INDEX `plan_id` ON `landlord_subscriptions` (`plan_id`);

CREATE INDEX `last_payment_id` ON `landlord_subscriptions` (`last_payment_id`);

CREATE INDEX `idx_landlord_status` ON `landlord_subscriptions` (`landlord_id`, `status`);

CREATE INDEX `idx_end_date` ON `landlord_subscriptions` (`end_date`);

CREATE INDEX `idx_verification_status` ON `landlords` (`identity_verification_status`);

CREATE INDEX `current_plan_id` ON `landlords` (`current_plan_id`);

CREATE INDEX `idx_subscription_status` ON `landlords` (`subscription_status`);

CREATE INDEX `idx_location` ON `nearby_attractions` (`location`);

CREATE INDEX `idx_user_read` ON `notifications` (`user_id`, `is_read`);

CREATE INDEX `idx_user_created` ON `notifications` (`user_id`, `created_at`);

CREATE INDEX `idx_type` ON `notifications` (`type`);

CREATE INDEX `idx_reference` ON `notifications` (`reference_type`, `reference_id`);

CREATE INDEX `idx_apartment` ON `packages` (`apartment_id`);

CREATE INDEX `idx_active` ON `packages` (`is_active`);

CREATE INDEX `idx_entity` ON `payments` (`related_entity_type`, `related_entity_id`);

CREATE INDEX `idx_purpose` ON `payments` (`payment_purpose`);

CREATE INDEX `idx_status` ON `payments` (`status`);

CREATE INDEX `approved_by` ON `property_inspections` (`approved_by`);

CREATE INDEX `idx_apartment` ON `property_inspections` (`apartment_id`);

CREATE INDEX `idx_status` ON `property_inspections` (`status`);

CREATE INDEX `idx_inspector` ON `property_inspections` (`inspector_id`);

CREATE UNIQUE INDEX `uk_booking_review` ON `reviews` (`booking_id`, `reviewer_id`);

CREATE INDEX `reviewer_id` ON `reviews` (`reviewer_id`);

CREATE INDEX `reviewed_id` ON `reviews` (`reviewed_id`);

CREATE INDEX `apartment_id` ON `reviews` (`apartment_id`);

CREATE UNIQUE INDEX `uk_room_in_apartment` ON `rooms` (`apartment_id`);

CREATE INDEX `idx_apartment` ON `rooms` (`apartment_id`);

CREATE INDEX `idx_apartment_date` ON `smart_pricing_history` (`apartment_id`, `date`);

CREATE INDEX `idx_active` ON `subscription_plans` (`is_active`);

CREATE UNIQUE INDEX `uk_assignment` ON `support_ticket_assignments` (`ticket_id`, `staff_id`);

CREATE INDEX `staff_id` ON `support_ticket_assignments` (`staff_id`);

CREATE INDEX `resolved_by` ON `support_tickets` (`resolved_by`);

CREATE INDEX `idx_status` ON `support_tickets` (`status`);

CREATE INDEX `idx_priority` ON `support_tickets` (`priority`);

CREATE INDEX `idx_category` ON `support_tickets` (`category`);

CREATE INDEX `idx_user` ON `support_tickets` (`user_id`);

CREATE UNIQUE INDEX `uk_booking` ON `temporary_residence_reports` (`booking_id`);

CREATE INDEX `landlord_id` ON `temporary_residence_reports` (`landlord_id`);

CREATE INDEX `idx_verification_status` ON `tenants` (`identity_verification_status`);

CREATE INDEX `idx_user` ON `user_identity_documents` (`user_id`);

CREATE INDEX `idx_status` ON `user_identity_documents` (`verification_status`);

CREATE INDEX `idx_type` ON `user_identity_documents` (`document_type`);

CREATE UNIQUE INDEX `email` ON `users` (`email`);

CREATE INDEX `idx_role` ON `users` (`role`);

CREATE INDEX `idx_email` ON `users` (`email`);

ALTER TABLE `admin_actions` ADD CONSTRAINT `admin_actions_ibfk_1` FOREIGN KEY (`admin_id`) REFERENCES `users` (`user_id`) ON DELETE CASCADE;

ALTER TABLE `apartment_amenities` ADD CONSTRAINT `apartment_amenities_ibfk_1` FOREIGN KEY (`apartment_id`) REFERENCES `apartments` (`apartment_id`) ON DELETE CASCADE;

ALTER TABLE `apartment_amenities` ADD CONSTRAINT `apartment_amenities_ibfk_2` FOREIGN KEY (`amenity_id`) REFERENCES `amenities` (`amenity_id`) ON DELETE CASCADE;

ALTER TABLE `apartment_media` ADD CONSTRAINT `apartment_media_ibfk_1` FOREIGN KEY (`apartment_id`) REFERENCES `apartments` (`apartment_id`) ON DELETE CASCADE;

ALTER TABLE `apartment_availability` ADD CONSTRAINT `apartment_availability_ibfk_1` FOREIGN KEY (`apartment_id`) REFERENCES `apartments` (`apartment_id`) ON DELETE CASCADE;

ALTER TABLE `apartment_price_calendar` ADD CONSTRAINT `apartment_price_calendar_ibfk_1` FOREIGN KEY (`apartment_id`) REFERENCES `apartments` (`apartment_id`) ON DELETE CASCADE;

ALTER TABLE `apartments` ADD CONSTRAINT `apartments_ibfk_1` FOREIGN KEY (`landlord_id`) REFERENCES `landlords` (`landlord_id`) ON DELETE CASCADE;

ALTER TABLE `bookings` ADD CONSTRAINT `bookings_ibfk_1` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`tenant_id`) ON DELETE CASCADE;

ALTER TABLE `bookings` ADD CONSTRAINT `bookings_ibfk_2` FOREIGN KEY (`apartment_id`) REFERENCES `apartments` (`apartment_id`) ON DELETE CASCADE;

ALTER TABLE `bookings` ADD CONSTRAINT `bookings_ibfk_3` FOREIGN KEY (`package_id`) REFERENCES `packages` (`package_id`) ON DELETE SET NULL;

ALTER TABLE `inspection_photos` ADD CONSTRAINT `inspection_photos_ibfk_1` FOREIGN KEY (`inspection_id`) REFERENCES `property_inspections` (`inspection_id`) ON DELETE CASCADE;

ALTER TABLE `landlord_subscriptions` ADD CONSTRAINT `landlord_subscriptions_ibfk_1` FOREIGN KEY (`landlord_id`) REFERENCES `landlords` (`landlord_id`) ON DELETE CASCADE;

ALTER TABLE `landlord_subscriptions` ADD CONSTRAINT `landlord_subscriptions_ibfk_2` FOREIGN KEY (`plan_id`) REFERENCES `subscription_plans` (`plan_id`) ON DELETE RESTRICT;

ALTER TABLE `landlord_subscriptions` ADD CONSTRAINT `landlord_subscriptions_ibfk_3` FOREIGN KEY (`last_payment_id`) REFERENCES `payments` (`payment_id`) ON DELETE SET NULL;

ALTER TABLE `landlords` ADD CONSTRAINT `landlords_ibfk_1` FOREIGN KEY (`landlord_id`) REFERENCES `users` (`user_id`) ON DELETE CASCADE;

ALTER TABLE `landlords` ADD CONSTRAINT `landlords_ibfk_2` FOREIGN KEY (`current_plan_id`) REFERENCES `subscription_plans` (`plan_id`) ON DELETE SET NULL;

ALTER TABLE `notifications` ADD CONSTRAINT `notifications_ibfk_1` FOREIGN KEY (`user_id`) REFERENCES `users` (`user_id`) ON DELETE CASCADE;

ALTER TABLE `packages` ADD CONSTRAINT `packages_ibfk_1` FOREIGN KEY (`apartment_id`) REFERENCES `apartments` (`apartment_id`) ON DELETE CASCADE;

ALTER TABLE `property_inspections` ADD CONSTRAINT `property_inspections_ibfk_1` FOREIGN KEY (`apartment_id`) REFERENCES `apartments` (`apartment_id`) ON DELETE CASCADE;

ALTER TABLE `property_inspections` ADD CONSTRAINT `property_inspections_ibfk_2` FOREIGN KEY (`inspector_id`) REFERENCES `users` (`user_id`) ON DELETE CASCADE;

ALTER TABLE `property_inspections` ADD CONSTRAINT `property_inspections_ibfk_3` FOREIGN KEY (`approved_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL;

ALTER TABLE `reviews` ADD CONSTRAINT `reviews_ibfk_1` FOREIGN KEY (`booking_id`) REFERENCES `bookings` (`booking_id`) ON DELETE CASCADE;

ALTER TABLE `reviews` ADD CONSTRAINT `reviews_ibfk_2` FOREIGN KEY (`reviewer_id`) REFERENCES `users` (`user_id`) ON DELETE CASCADE;

ALTER TABLE `reviews` ADD CONSTRAINT `reviews_ibfk_3` FOREIGN KEY (`reviewed_id`) REFERENCES `users` (`user_id`) ON DELETE CASCADE;

ALTER TABLE `reviews` ADD CONSTRAINT `reviews_ibfk_4` FOREIGN KEY (`apartment_id`) REFERENCES `apartments` (`apartment_id`) ON DELETE SET NULL;

ALTER TABLE `rooms` ADD CONSTRAINT `rooms_ibfk_1` FOREIGN KEY (`apartment_id`) REFERENCES `apartments` (`apartment_id`) ON DELETE CASCADE;

ALTER TABLE `smart_pricing_history` ADD CONSTRAINT `smart_pricing_history_ibfk_1` FOREIGN KEY (`apartment_id`) REFERENCES `apartments` (`apartment_id`) ON DELETE CASCADE;

ALTER TABLE `support_ticket_assignments` ADD CONSTRAINT `support_ticket_assignments_ibfk_1` FOREIGN KEY (`ticket_id`) REFERENCES `support_tickets` (`ticket_id`) ON DELETE CASCADE;

ALTER TABLE `support_ticket_assignments` ADD CONSTRAINT `support_ticket_assignments_ibfk_2` FOREIGN KEY (`staff_id`) REFERENCES `users` (`user_id`) ON DELETE CASCADE;

ALTER TABLE `support_tickets` ADD CONSTRAINT `support_tickets_ibfk_1` FOREIGN KEY (`user_id`) REFERENCES `users` (`user_id`) ON DELETE CASCADE;

ALTER TABLE `support_tickets` ADD CONSTRAINT `support_tickets_ibfk_2` FOREIGN KEY (`resolved_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL;

ALTER TABLE `temporary_residence_reports` ADD CONSTRAINT `temporary_residence_reports_ibfk_1` FOREIGN KEY (`booking_id`) REFERENCES `bookings` (`booking_id`) ON DELETE CASCADE;

ALTER TABLE `temporary_residence_reports` ADD CONSTRAINT `temporary_residence_reports_ibfk_2` FOREIGN KEY (`landlord_id`) REFERENCES `landlords` (`landlord_id`) ON DELETE CASCADE;

ALTER TABLE `tenants` ADD CONSTRAINT `tenants_ibfk_1` FOREIGN KEY (`tenant_id`) REFERENCES `users` (`user_id`) ON DELETE CASCADE;

ALTER TABLE `user_identity_documents` ADD CONSTRAINT `user_identity_documents_ibfk_1` FOREIGN KEY (`user_id`) REFERENCES `users` (`user_id`) ON DELETE CASCADE;

ALTER TABLE `booking_check_times` ADD CONSTRAINT `booking_check_times_ibfk_1` FOREIGN KEY (`booking_id`) REFERENCES `bookings` (`booking_id`) ON DELETE CASCADE;

-- Create foreign key constraints from `package_packages` to `packages`
ALTER TABLE `package_packages` ADD CONSTRAINT `package_packages_ibfk_1` FOREIGN KEY (`package_id`) REFERENCES `packages` (`package_id`);

-- Create foreign key constraints from `package_packages` to `package_items`
ALTER TABLE `package_packages` ADD CONSTRAINT `package_packages_ibfk_2` FOREIGN KEY (`package_item_id`) REFERENCES `package_items` (`package_item_id`);

ALTER TABLE `momo_transactions` ADD CONSTRAINT `fk_momo_transactions_payment` FOREIGN KEY (`payment_id`) REFERENCES `payments`(`payment_id`);