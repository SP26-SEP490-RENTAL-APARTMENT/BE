-- phpMyAdmin SQL Dump
-- version 5.2.1
-- https://www.phpmyadmin.net/
--
-- Host: 178.63.129.221
-- Generation Time: Apr 19, 2026 at 10:04 AM
-- Server version: 10.11.15-MariaDB-log
-- PHP Version: 8.2.25

SET SQL_MODE = "NO_AUTO_VALUE_ON_ZERO";
START TRANSACTION;
SET time_zone = "+00:00";


/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!40101 SET NAMES utf8mb4 */;

--
-- Database: `db30545`
--

-- --------------------------------------------------------

--
-- Table structure for table `admin_actions`
--
drop database `ApartmentDb`;

create database `ApartmentDb`;

use `ApartmentDb`;
CREATE TABLE `admin_actions` (
  `action_id` char(36) NOT NULL DEFAULT (uuid()),
  `admin_id` char(36) NOT NULL,
  `action_type` varchar(50) NOT NULL,
  `target_type` enum('user','apartment','booking','review','report') NOT NULL,
  `target_id` char(36) NOT NULL,
  `description` varchar(500) DEFAULT NULL,
  `previous_value` text DEFAULT NULL,
  `new_value` text DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT current_timestamp(),
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `amenities`
--

CREATE TABLE `amenities` (
  `amenity_id` char(36) NOT NULL DEFAULT (uuid()),
  `name_en` varchar(100) NOT NULL,
  `name_vi` varchar(100) NOT NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `apartments`
--

CREATE TABLE `apartments` (
  `apartment_id` char(36) NOT NULL DEFAULT (uuid()),
  `landlord_id` char(36) NOT NULL,
  `title` varchar(200) NOT NULL,
  `description` text DEFAULT NULL,
  `max_occupants` tinyint(4) DEFAULT 1,
  `max_pets` tinyint(4) DEFAULT NULL,
  `is_pet_allowed` tinyint(1) DEFAULT 0,
  `address` varchar(255) DEFAULT NULL,
  `district` varchar(100) DEFAULT NULL,
  `city` varchar(100) DEFAULT 'Hồ Chí Minh',
  `latitude` decimal(10,8) DEFAULT NULL,
  `longitude` decimal(11,8) DEFAULT NULL,
  `location` point NOT NULL,
  `base_price_per_night` decimal(12,2) NOT NULL,
  `status` enum('draft','pending_review','posted','blocked','archived') DEFAULT 'draft',
  `booking_status` enum('available','confirmed','locked') DEFAULT 'available',
  `created_at` timestamp NULL DEFAULT current_timestamp(),
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `apartment_amenities`
--

CREATE TABLE `apartment_amenities` (
  `apartment_id` char(36) NOT NULL,
  `amenity_id` char(36) NOT NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `apartment_availability`
--

CREATE TABLE `apartment_availability` (
  `availability_id` char(36) NOT NULL DEFAULT (uuid()),
  `apartment_id` char(36) NOT NULL,
  `start_date` date NOT NULL,
  `end_date` date NOT NULL,
  `reason` varchar(200) DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT current_timestamp(),
  `updated_at` timestamp NULL DEFAULT current_timestamp(),
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `apartment_media`
--

CREATE TABLE `apartment_media` (
  `media_id` char(36) NOT NULL DEFAULT (uuid()),
  `apartment_id` char(36) NOT NULL,
  `url` varchar(500) NOT NULL,
  `type` enum('photo','video') NOT NULL,
  `is_primary` tinyint(1) DEFAULT 0,
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `apartment_price_calendar`
--

CREATE TABLE `apartment_price_calendar` (
  `price_id` char(36) NOT NULL DEFAULT (uuid()),
  `apartment_id` char(36) NOT NULL,
  `start_date` date NOT NULL,
  `end_date` date NOT NULL,
  `discount_percentage` decimal(5,2) DEFAULT 0.00,
  `is_discount` tinyint(1) DEFAULT 0,
  `price_type` enum('base','weekend','holiday','peak_season','low_season','special_event','manual_override') DEFAULT 'base',
  `min_nights` int(11) DEFAULT 1,
  `created_at` timestamp NULL DEFAULT current_timestamp(),
  `updated_at` timestamp NULL DEFAULT current_timestamp(),
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `bookings`
--

CREATE TABLE `bookings` (
  `booking_id` char(36) NOT NULL DEFAULT (uuid()),
  `tenant_id` char(36) NOT NULL,
  `apartment_id` char(36) NOT NULL,
  `check_in_date` date NOT NULL,
  `check_out_date` date NOT NULL,
  `nights` int(11) NOT NULL,
  `noOfAdults` int(11) DEFAULT NULL,
  `noOfInfants` int(11) DEFAULT NULL,
  `noOfPets` int(11) DEFAULT NULL,
  `total_price` decimal(12,2) NOT NULL,
  `package_id` char(36) DEFAULT NULL,
  `package_price` decimal(12,2) DEFAULT 0.00,
  `deposit_amount` decimal(12,2) NOT NULL,
  `payment_mode` enum('partial','full') NOT NULL DEFAULT 'partial',
  `upfront_payment_amount` decimal(12,2) NOT NULL DEFAULT 0.00,
  `deposit_paid` tinyint(1) DEFAULT 0,
  `balance_due_date` date NOT NULL,
  `status` enum('pending','negotiating','confirmed','paid','completed','cancelled','disputed') DEFAULT 'pending',
  `created_at` timestamp NULL DEFAULT current_timestamp(),
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `booking_check_times`
--

CREATE TABLE `booking_check_times` (
  `check_time_id` char(36) NOT NULL DEFAULT (uuid()),
  `booking_id` char(36) NOT NULL,
  `scheduled_check_in` datetime NOT NULL,
  `scheduled_check_out` datetime NOT NULL,
  `actual_check_in` datetime DEFAULT NULL,
  `actual_check_out` datetime DEFAULT NULL,
  `is_late_check_out` tinyint(1) DEFAULT 0,
  `late_check_out_fee` decimal(12,2) DEFAULT 0.00,
  `is_early_check_in` tinyint(1) DEFAULT 0,
  `early_check_in_fee` decimal(12,2) DEFAULT 0.00,
  `fee_settlement_status` varchar(30) NOT NULL DEFAULT 'none',
  `fee_due_at` datetime DEFAULT NULL,
  `fee_settled_at` datetime DEFAULT NULL,
  `fee_settlement_notes` text DEFAULT NULL,
  `temp_residence_reported` tinyint(1) DEFAULT 0,
  `reported_at` datetime DEFAULT NULL,
  `report_reference` varchar(100) DEFAULT NULL,
  `recorded_by` char(36) DEFAULT NULL,
  `recorded_at` datetime DEFAULT NULL,
  `notes` text DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT current_timestamp(),
  `updated_at` timestamp NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  `tenant_response_status` varchar(50) DEFAULT NULL,
  `tenant_responded_by` char(36) DEFAULT NULL,
  `tenant_responded_at` datetime DEFAULT NULL,
  `tenant_dispute_reason` varchar(300) DEFAULT NULL,
  `tenant_dispute_notes` text DEFAULT NULL,
  `dispute_resolution_status` varchar(80) DEFAULT NULL,
  `dispute_resolved_by` char(36) DEFAULT NULL,
  `dispute_resolved_at` datetime DEFAULT NULL,
  `dispute_resolution_notes` text DEFAULT NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `booking_occupants`
--

CREATE TABLE `booking_occupants` (
  `occupant_id` char(36) NOT NULL,
  `booking_id` char(36) NOT NULL,
  `occupant_order` int(11) NOT NULL,
  `is_primary` tinyint(1) NOT NULL DEFAULT 0,
  `full_name` varchar(150) DEFAULT NULL,
  `passport_id` varchar(50) DEFAULT NULL,
  `national_id_card_number` varchar(20) DEFAULT NULL,
  `nationality` char(2) DEFAULT NULL,
  `date_of_birth` date DEFAULT NULL,
  `sex` varchar(20) DEFAULT NULL,
  `phone` varchar(20) DEFAULT NULL,
  `email` varchar(255) DEFAULT NULL,
  `proof_photo_url` varchar(1000) DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT current_timestamp(),
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `booking_offers`
--

CREATE TABLE `booking_offers` (
  `offer_id` char(36) NOT NULL DEFAULT (uuid()),
  `original_booking_id` char(36) NOT NULL,
  `alternative_apartment_id` char(36) NOT NULL,
  `tenant_id` char(36) NOT NULL,
  `created_by_staff_id` char(36) DEFAULT NULL,
  `original_price` decimal(12,2) NOT NULL,
  `alternative_price` decimal(12,2) NOT NULL,
  `price_difference` decimal(12,2) NOT NULL,
  `status` enum('pending','accepted','rejected','expired','cancelled') NOT NULL DEFAULT 'pending',
  `reason` varchar(100) DEFAULT NULL,
  `expires_at` timestamp NULL DEFAULT NULL,
  `created_at` timestamp NOT NULL DEFAULT current_timestamp(),
  `responded_at` timestamp NULL DEFAULT NULL,
  `tenant_response_notes` text DEFAULT NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `generated_reports`
--

CREATE TABLE `generated_reports` (
  `generated_report_id` char(36) NOT NULL,
  `report_id` char(36) NOT NULL,
  `requested_by` char(36) NOT NULL,
  `requested_at` datetime NOT NULL DEFAULT current_timestamp(),
  `status` varchar(50) NOT NULL DEFAULT 'completed',
  `result_summary_json` longtext DEFAULT NULL,
  `result_json` longtext DEFAULT NULL,
  `retention_until` datetime DEFAULT NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `holidays_events`
--

CREATE TABLE `holidays_events` (
  `event_id` char(36) NOT NULL DEFAULT (uuid()),
  `event_name` varchar(150) NOT NULL,
  `event_type` enum('national_holiday','local_festival','international_event','school_break','major_conference','sports_event','other') NOT NULL,
  `start_date` date NOT NULL,
  `end_date` date NOT NULL,
  `location_scope` varchar(100) DEFAULT 'Vietnam',
  `description` text DEFAULT NULL,
  `is_recurring` tinyint(1) DEFAULT 0,
  `recurrence_rule` varchar(255) DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT current_timestamp(),
  `updated_at` timestamp NULL DEFAULT current_timestamp(),
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `inspection_photos`
--

CREATE TABLE `inspection_photos` (
  `photo_id` char(36) NOT NULL DEFAULT (uuid()),
  `inspection_id` char(36) NOT NULL,
  `file_url` varchar(500) NOT NULL,
  `file_key` varchar(255) DEFAULT NULL,
  `description` varchar(200) DEFAULT NULL,
  `is_issue` tinyint(1) DEFAULT 0,
  `uploaded_at` timestamp NULL DEFAULT current_timestamp(),
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `landlords`
--

CREATE TABLE `landlords` (
  `landlord_id` char(36) NOT NULL,
  `verified_business` tinyint(1) DEFAULT 0,
  `current_plan_id` char(36) DEFAULT NULL,
  `subscription_expires_at` date DEFAULT NULL,
  `identity_verification_status` enum('not_started','pending','verified','rejected') DEFAULT 'not_started',
  `last_verified_at` timestamp NULL DEFAULT NULL,
  `subscription_status` enum('none','active','expired','pending') DEFAULT 'none',
  `momo_wallet_phone` varchar(20) DEFAULT NULL,
  `payout_receiver_name` varchar(150) DEFAULT NULL,
  `payout_personal_id` varchar(30) DEFAULT NULL,
  `payout_bank_account_no` varchar(40) DEFAULT NULL,
  `payout_bank_card_no` varchar(40) DEFAULT NULL,
  `payout_bank_code` varchar(20) DEFAULT NULL,
  `preferred_payout_method` varchar(20) DEFAULT NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `landlord_payouts`
--

CREATE TABLE `landlord_payouts` (
  `payout_id` char(36) NOT NULL,
  `landlord_id` char(36) NOT NULL,
  `amount` bigint(20) NOT NULL,
  `fee_amount` bigint(20) NOT NULL DEFAULT 0,
  `net_amount` bigint(20) NOT NULL,
  `channel` varchar(20) NOT NULL,
  `status` varchar(30) NOT NULL,
  `momo_order_id` varchar(120) NOT NULL,
  `momo_request_id` varchar(120) NOT NULL,
  `momo_trans_id` varchar(120) DEFAULT NULL,
  `result_code` int(11) DEFAULT NULL,
  `message` varchar(1024) DEFAULT NULL,
  `request_body` longtext DEFAULT NULL,
  `response_body` longtext DEFAULT NULL,
  `created_at` timestamp NOT NULL DEFAULT current_timestamp(),
  `updated_at` timestamp NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  `completed_at` timestamp NULL DEFAULT NULL,
  `failed_at` timestamp NULL DEFAULT NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `landlord_subscriptions`
--

CREATE TABLE `landlord_subscriptions` (
  `subscription_id` char(36) NOT NULL DEFAULT (uuid()),
  `landlord_id` char(36) NOT NULL,
  `plan_id` char(36) NOT NULL,
  `status` enum('active','pending_payment','expired','cancelled','trial') DEFAULT 'pending_payment',
  `start_date` date NOT NULL,
  `end_date` date DEFAULT NULL,
  `renewal_type` enum('monthly','annual','none') DEFAULT 'monthly',
  `auto_renew` tinyint(1) DEFAULT 1,
  `payment_method` varchar(50) DEFAULT NULL,
  `last_payment_id` char(36) DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT current_timestamp(),
  `updated_at` timestamp NULL DEFAULT current_timestamp(),
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `landlord_wallets`
--

CREATE TABLE `landlord_wallets` (
  `landlord_id` char(36) NOT NULL,
  `pending_balance` decimal(12,2) NOT NULL DEFAULT 0.00,
  `available_balance` decimal(12,2) NOT NULL DEFAULT 0.00,
  `updated_at` timestamp NULL DEFAULT NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `momo_transactions`
--

CREATE TABLE `momo_transactions` (
  `id` int(11) NOT NULL,
  `request_id` varchar(100) NOT NULL,
  `partner_code` varchar(50) NOT NULL,
  `amount` bigint(20) NOT NULL,
  `type` varchar(50) NOT NULL,
  `request_body` longtext NOT NULL,
  `response_body` longtext NOT NULL DEFAULT '',
  `status` varchar(50) NOT NULL,
  `result_code` int(11) DEFAULT NULL,
  `message` varchar(1024) NOT NULL DEFAULT '',
  `created_at` datetime(6) NOT NULL DEFAULT current_timestamp(6),
  `updated_at` datetime(6) NOT NULL DEFAULT current_timestamp(6) ON UPDATE current_timestamp(6),
  `payment_id` char(36) DEFAULT NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `nearby_attractions`
--

CREATE TABLE `nearby_attractions` (
  `attraction_id` char(36) NOT NULL DEFAULT (uuid()),
  `name_en` varchar(200) NOT NULL,
  `name_vi` varchar(200) NOT NULL,
  `type` enum('restaurant','museum','park','landmark','shopping','transport') NOT NULL,
  `location` point NOT NULL,
  `address` varchar(500) DEFAULT NULL,
  `city` varchar(100) DEFAULT NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `notifications`
--

CREATE TABLE `notifications` (
  `notification_id` char(36) NOT NULL DEFAULT (uuid()),
  `user_id` char(36) NOT NULL,
  `type` enum('booking_created','booking_confirmed','booking_cancelled','booking_upcoming','payment_success','payment_failed','identity_verified','identity_rejected','listing_approved','listing_rejected','inspection_scheduled','inspection_completed','support_ticket_created','support_ticket_update','support_ticket_resolved','review_reminder','new_message','system_announcement','other','check_in_recorded','check_out_recorded','check_time_confirmed','check_time_disputed','check_time_dispute_resolved') NOT NULL,
  `title` varchar(150) NOT NULL,
  `message` text NOT NULL,
  `reference_id` char(36) DEFAULT NULL,
  `reference_type` varchar(50) DEFAULT NULL,
  `is_read` tinyint(1) DEFAULT 0,
  `read_at` timestamp NULL DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT current_timestamp(),
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `packages`
--

CREATE TABLE `packages` (
  `package_id` char(36) NOT NULL DEFAULT (uuid()),
  `apartment_id` char(36) NOT NULL,
  `name` varchar(150) NOT NULL,
  `description` text DEFAULT NULL,
  `price` decimal(12,2) NOT NULL,
  `currency` varchar(3) DEFAULT 'VND',
  `is_active` tinyint(1) DEFAULT 1,
  `max_bookings` int(11) DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT current_timestamp(),
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `package_items`
--

CREATE TABLE `package_items` (
  `package_item_id` char(36) NOT NULL DEFAULT (uuid()),
  `item_name` varchar(150) NOT NULL,
  `item_description` text DEFAULT NULL,
  `quantity` decimal(10,2) DEFAULT 1.00,
  `estimated_value` decimal(12,2) DEFAULT 0.00,
  `sort_order` int(11) DEFAULT 0,
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `package_packages`
--

CREATE TABLE `package_packages` (
  `id` char(36) NOT NULL DEFAULT (uuid()),
  `package_id` char(36) NOT NULL,
  `package_item_id` char(36) NOT NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `payments`
--

CREATE TABLE `payments` (
  `payment_id` char(36) NOT NULL DEFAULT (uuid()),
  `related_entity_id` char(36) DEFAULT NULL,
  `amount` decimal(12,2) NOT NULL,
  `payment_type` enum('deposit','balance','addon','refund','upfront') NOT NULL,
  `payment_purpose` enum('booking_deposit','booking_balance','booking_full_payment','booking_addon_or_package','subscription_monthly','subscription_annual','subscription_trial','subscription_renewal','refund_booking','refund_subscription','other') NOT NULL DEFAULT 'booking_deposit',
  `related_entity_type` enum('booking','host_subscription','other') DEFAULT NULL,
  `landlord_id` char(36) DEFAULT NULL,
  `landlord_amount` decimal(12,2) NOT NULL DEFAULT 0.00,
  `platform_fee` decimal(12,2) NOT NULL DEFAULT 0.00 COMMENT 'Platform fee amount',
  `settlement_status` varchar(20) NOT NULL DEFAULT 'pending',
  `method` varchar(50) NOT NULL,
  `status` enum('pending','success','failed','refunded') DEFAULT 'pending',
  `transaction_id` varchar(100) DEFAULT NULL,
  `paid_at` timestamp NULL DEFAULT NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `property_inspections`
--

CREATE TABLE `property_inspections` (
  `inspection_id` char(36) NOT NULL DEFAULT (uuid()),
  `apartment_id` char(36) NOT NULL,
  `inspector_id` char(36) NOT NULL,
  `scheduled_date` date DEFAULT NULL,
  `completed_date` date DEFAULT NULL,
  `status` enum('pending','scheduled','in_progress','passed','failed','re_inspection_needed') DEFAULT 'pending',
  `overall_condition` text DEFAULT NULL,
  `issues_found` text DEFAULT NULL,
  `recommendations` text DEFAULT NULL,
  `approved_for_listing` tinyint(1) DEFAULT 0,
  `approved_at` timestamp NULL DEFAULT NULL,
  `approved_by` char(36) DEFAULT NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `report_definitions`
--

CREATE TABLE `report_definitions` (
  `report_id` char(36) NOT NULL,
  `name` varchar(150) NOT NULL,
  `description` text DEFAULT NULL,
  `type` enum('standard','custom','scheduled','real_time') NOT NULL DEFAULT 'standard',
  `category` varchar(100) NOT NULL DEFAULT '',
  `is_active` tinyint(1) NOT NULL DEFAULT 1,
  `created_by` char(36) DEFAULT NULL,
  `created_at` timestamp NOT NULL DEFAULT current_timestamp(),
  `updated_at` timestamp NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `report_query_configs`
--

CREATE TABLE `report_query_configs` (
  `report_id` char(36) NOT NULL,
  `dimensions_json` longtext DEFAULT NULL,
  `filters_json` longtext DEFAULT NULL,
  `metrics_json` longtext DEFAULT NULL,
  `time_range_json` longtext DEFAULT NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `reviews`
--

CREATE TABLE `reviews` (
  `review_id` char(36) NOT NULL DEFAULT (uuid()),
  `booking_id` char(36) NOT NULL,
  `reviewer_id` char(36) NOT NULL,
  `reviewed_id` char(36) NOT NULL,
  `apartment_id` char(36) DEFAULT NULL,
  `rating` tinyint(4) DEFAULT NULL,
  `comment_en` text DEFAULT NULL,
  `comment_vi` text DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT current_timestamp(),
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `rooms`
--

CREATE TABLE `rooms` (
  `room_id` char(36) NOT NULL DEFAULT (uuid()),
  `apartment_id` char(36) NOT NULL,
  `title` varchar(150) DEFAULT NULL,
  `description` text DEFAULT NULL,
  `room_type` enum('private_single','private_double','shared_bed','studio','other') DEFAULT 'private_single',
  `bed_type` enum('single','double','queen','king','bunk','shared') DEFAULT 'single',
  `size_sqm` decimal(5,2) DEFAULT NULL,
  `is_private_bathroom` tinyint(1) DEFAULT 0,
  `created_at` timestamp NULL DEFAULT current_timestamp(),
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `scheduled_reports`
--

CREATE TABLE `scheduled_reports` (
  `scheduled_report_id` char(36) NOT NULL,
  `report_id` char(36) NOT NULL,
  `frequency` varchar(50) DEFAULT NULL,
  `cron_expression` varchar(100) DEFAULT NULL,
  `next_run_at` datetime DEFAULT NULL,
  `last_run_at` datetime DEFAULT NULL,
  `delivery_channel` varchar(50) DEFAULT NULL,
  `recipients` text DEFAULT NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `smart_pricing_history`
--

CREATE TABLE `smart_pricing_history` (
  `pricing_id` char(36) NOT NULL DEFAULT (uuid()),
  `apartment_id` char(36) NOT NULL,
  `date` date NOT NULL,
  `suggested_price` decimal(12,2) NOT NULL,
  `base_price` decimal(12,2) NOT NULL,
  `multiplier` decimal(5,2) DEFAULT 1.00,
  `reason` varchar(255) DEFAULT NULL,
  `occupancy_rate` decimal(5,2) DEFAULT NULL,
  `accepted_by_landlord` tinyint(1) DEFAULT 0,
  `created_at` timestamp NULL DEFAULT current_timestamp(),
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `subscription_plans`
--

CREATE TABLE `subscription_plans` (
  `plan_id` char(36) NOT NULL DEFAULT (uuid()),
  `name` varchar(100) NOT NULL,
  `description` text DEFAULT NULL,
  `price_monthly` decimal(12,2) NOT NULL,
  `price_annual` decimal(12,2) DEFAULT NULL,
  `max_apartments` int(11) DEFAULT 1,
  `features` text DEFAULT NULL,
  `is_active` tinyint(1) DEFAULT 1,
  `created_at` timestamp NULL DEFAULT current_timestamp(),
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `support_tickets`
--

CREATE TABLE `support_tickets` (
  `ticket_id` char(36) NOT NULL DEFAULT (uuid()),
  `user_id` char(36) NOT NULL,
  `subject` varchar(200) NOT NULL,
  `description` text NOT NULL,
  `category` enum('booking_issue','payment_problem','listing_problem','account_verification','cancellation','dispute','property_quality','other') NOT NULL,
  `priority` enum('low','medium','high','urgent') DEFAULT 'medium',
  `status` enum('open','in_progress','resolved','closed','escalated') DEFAULT 'open',
  `created_at` timestamp NULL DEFAULT current_timestamp(),
  `updated_at` timestamp NULL DEFAULT current_timestamp(),
  `resolved_at` timestamp NULL DEFAULT NULL,
  `resolved_by` char(36) DEFAULT NULL,
  `resolution_notes` text DEFAULT NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `support_ticket_assignments`
--

CREATE TABLE `support_ticket_assignments` (
  `assignment_id` char(36) NOT NULL DEFAULT (uuid()),
  `ticket_id` char(36) NOT NULL,
  `staff_id` char(36) NOT NULL,
  `assigned_at` timestamp NULL DEFAULT current_timestamp(),
  `role_in_ticket` enum('primary','collaborator') DEFAULT 'primary',
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `temporary_residence_reports`
--

CREATE TABLE `temporary_residence_reports` (
  `report_id` char(36) NOT NULL DEFAULT (uuid()),
  `booking_id` char(36) NOT NULL,
  `landlord_id` char(36) NOT NULL,
  `tenant_passport_id` varchar(50) NOT NULL,
  `tenant_nationality` varchar(100) NOT NULL,
  `check_in_date` date NOT NULL,
  `reported_to_police` tinyint(1) DEFAULT 0,
  `report_date` date DEFAULT NULL,
  `report_number` varchar(100) DEFAULT NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `tenants`
--

CREATE TABLE `tenants` (
  `tenant_id` char(36) NOT NULL,
  `passport_id` varchar(50) DEFAULT NULL,
  `identity_verification_status` enum('not_started','pending','verified','rejected') DEFAULT 'not_started',
  `last_verified_at` timestamp NULL DEFAULT NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `tenant_wishlists`
--

CREATE TABLE `tenant_wishlists` (
  `wishlist_id` char(36) NOT NULL,
  `tenant_id` char(36) NOT NULL,
  `apartment_id` char(36) NOT NULL,
  `collection_id` char(36) NOT NULL,
  `is_favorite` tinyint(1) NOT NULL DEFAULT 0,
  `notes` varchar(500) DEFAULT NULL,
  `created_at` timestamp NOT NULL DEFAULT current_timestamp(),
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `users`
--

CREATE TABLE `users` (
  `user_id` char(36) NOT NULL DEFAULT (uuid()),
  `email` varchar(255) NOT NULL,
  `password_hash` varchar(255) NOT NULL,
  `role` enum('tenant','landlord','admin','staff') NOT NULL,
  `full_name` varchar(100) DEFAULT NULL,
  `phone` varchar(20) DEFAULT NULL,
  `identity_verified` tinyint(1) DEFAULT 0,
  `created_at` timestamp NULL DEFAULT current_timestamp(),
  `token` varchar(500) DEFAULT NULL COMMENT 'User access token',
  `token_expired` datetime DEFAULT NULL COMMENT 'Token expiration time',
  `sex` varchar(20) DEFAULT NULL,
  `birthday` date DEFAULT NULL,
  `nationality` char(2) DEFAULT NULL COMMENT 'ISO 3166-1 alpha-2 code (e.g. VN, US, KR). Used for temp residence reporting',
  `national_id_card_number` varchar(12) DEFAULT NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `user_identity_documents`
--

CREATE TABLE `user_identity_documents` (
  `document_id` char(36) NOT NULL DEFAULT (uuid()),
  `user_id` char(36) NOT NULL,
  `document_type` enum('passport','national_id_card','drivers_license','other_government_id','selfie_with_id') NOT NULL,
  `side` enum('front','back','bio_page','other') DEFAULT 'front',
  `file_url` varchar(500) NOT NULL,
  `file_key` varchar(255) DEFAULT NULL,
  `mime_type` varchar(100) DEFAULT NULL,
  `file_size` bigint(20) DEFAULT NULL,
  `uploaded_at` timestamp NULL DEFAULT current_timestamp(),
  `verified_at` timestamp NULL DEFAULT NULL,
  `verification_status` enum('pending','verified','rejected','expired') DEFAULT 'pending',
  `rejection_reason` text DEFAULT NULL,
  `notes` text DEFAULT NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `identity_document_ocr_results`
--

CREATE TABLE `identity_document_ocr_results` (
  `ocr_result_id` char(36) NOT NULL DEFAULT (uuid()),
  `document_id` char(36) NOT NULL,
  `provider` varchar(50) NOT NULL,
  `provider_error_code` int DEFAULT NULL,
  `provider_error_message` text DEFAULT NULL,
  `card_type` varchar(50) DEFAULT NULL,
  `card_type_detail` varchar(50) DEFAULT NULL,
  `id_number` varchar(32) DEFAULT NULL,
  `full_name` varchar(150) DEFAULT NULL,
  `date_of_birth_raw` varchar(30) DEFAULT NULL,
  `issue_date_raw` varchar(30) DEFAULT NULL,
  `overall_confidence` decimal(5,4) DEFAULT NULL,
  `extracted_fields_json` longtext DEFAULT NULL,
  `field_confidences_json` longtext DEFAULT NULL,
  `auto_approved` tinyint(1) DEFAULT NULL,
  `match_passed` tinyint(1) DEFAULT NULL,
  `match_failure_reason` text DEFAULT NULL,
  `processed_at` timestamp NULL DEFAULT current_timestamp(),
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `wishlist_collections`
--

CREATE TABLE `wishlist_collections` (
  `collection_id` char(36) NOT NULL,
  `tenant_id` char(36) NOT NULL,
  `name` varchar(100) NOT NULL,
  `description` varchar(500) DEFAULT NULL,
  `is_default` tinyint(1) NOT NULL DEFAULT 0,
  `created_at` timestamp NOT NULL DEFAULT current_timestamp(),
  `updated_at` timestamp NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------

--
-- Table structure for table `__efmigrationshistory`
--

CREATE TABLE `__efmigrationshistory` (
  `MigrationId` varchar(150) NOT NULL,
  `ProductVersion` varchar(32) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

--
-- Indexes for dumped tables
--

--
-- Indexes for table `admin_actions`
--
ALTER TABLE `admin_actions`
  ADD PRIMARY KEY (`action_id`),
  ADD KEY `idx_admin` (`admin_id`),
  ADD KEY `idx_target` (`target_type`,`target_id`);

--
-- Indexes for table `amenities`
--
ALTER TABLE `amenities`
  ADD PRIMARY KEY (`amenity_id`),
  ADD UNIQUE KEY `uk_name_en` (`name_en`),
  ADD UNIQUE KEY `uk_name_vi` (`name_vi`);

--
-- Indexes for table `apartments`
--
ALTER TABLE `apartments`
  ADD PRIMARY KEY (`apartment_id`),
  ADD KEY `idx_landlord` (`landlord_id`),
  ADD KEY `idx_status` (`status`),
  ADD KEY `idx_booking_status` (`booking_status`),
  ADD KEY `idx_location` (`location`);

--
-- Indexes for table `apartment_amenities`
--
ALTER TABLE `apartment_amenities`
  ADD PRIMARY KEY (`apartment_id`,`amenity_id`),
  ADD KEY `amenity_id` (`amenity_id`);

--
-- Indexes for table `apartment_availability`
--
ALTER TABLE `apartment_availability`
  ADD PRIMARY KEY (`availability_id`),
  ADD KEY `idx_apartment_availability_dates` (`apartment_id`,`start_date`,`end_date`);

--
-- Indexes for table `apartment_media`
--
ALTER TABLE `apartment_media`
  ADD PRIMARY KEY (`media_id`),
  ADD KEY `idx_apartment` (`apartment_id`);

--
-- Indexes for table `apartment_price_calendar`
--
ALTER TABLE `apartment_price_calendar`
  ADD PRIMARY KEY (`price_id`),
  ADD UNIQUE KEY `uk_apartment_date_range` (`apartment_id`,`start_date`,`end_date`),
  ADD KEY `idx_apartment_dates` (`apartment_id`,`start_date`,`end_date`),
  ADD KEY `idx_dates` (`start_date`,`end_date`);

--
-- Indexes for table `bookings`
--
ALTER TABLE `bookings`
  ADD PRIMARY KEY (`booking_id`),
  ADD KEY `idx_tenant` (`tenant_id`),
  ADD KEY `idx_apartment` (`apartment_id`),
  ADD KEY `idx_status` (`status`),
  ADD KEY `idx_dates` (`check_in_date`,`check_out_date`),
  ADD KEY `idx_package` (`package_id`);

--
-- Indexes for table `booking_check_times`
--
ALTER TABLE `booking_check_times`
  ADD PRIMARY KEY (`check_time_id`),
  ADD UNIQUE KEY `uk_booking` (`booking_id`),
  ADD KEY `idx_booking` (`booking_id`),
  ADD KEY `idx_scheduled_in` (`scheduled_check_in`),
  ADD KEY `idx_actual_in` (`actual_check_in`);

--
-- Indexes for table `booking_occupants`
--
ALTER TABLE `booking_occupants`
  ADD PRIMARY KEY (`occupant_id`),
  ADD UNIQUE KEY `uk_booking_order` (`booking_id`,`occupant_order`),
  ADD KEY `idx_booking` (`booking_id`),
  ADD KEY `idx_booking_primary` (`booking_id`,`is_primary`);

--
-- Indexes for table `booking_offers`
--
ALTER TABLE `booking_offers`
  ADD PRIMARY KEY (`offer_id`),
  ADD KEY `idx_original_booking` (`original_booking_id`),
  ADD KEY `idx_alternative_apartment` (`alternative_apartment_id`),
  ADD KEY `idx_tenant` (`tenant_id`),
  ADD KEY `idx_status` (`status`),
  ADD KEY `idx_expires_at` (`expires_at`),
  ADD KEY `booking_offers_ibfk_4` (`created_by_staff_id`);

--
-- Indexes for table `generated_reports`
--
ALTER TABLE `generated_reports`
  ADD PRIMARY KEY (`generated_report_id`),
  ADD KEY `idx_generated_reports_report_id` (`report_id`),
  ADD KEY `idx_generated_reports_requested_by_requested_at` (`requested_by`,`requested_at`);

--
-- Indexes for table `holidays_events`
--
ALTER TABLE `holidays_events`
  ADD PRIMARY KEY (`event_id`),
  ADD UNIQUE KEY `uk_event_dates` (`event_name`,`start_date`,`end_date`),
  ADD KEY `idx_dates` (`start_date`,`end_date`),
  ADD KEY `idx_type_scope` (`event_type`,`location_scope`);

--
-- Indexes for table `inspection_photos`
--
ALTER TABLE `inspection_photos`
  ADD PRIMARY KEY (`photo_id`),
  ADD KEY `idx_inspection` (`inspection_id`);

--
-- Indexes for table `landlords`
--
ALTER TABLE `landlords`
  ADD PRIMARY KEY (`landlord_id`),
  ADD KEY `idx_verification_status` (`identity_verification_status`),
  ADD KEY `current_plan_id` (`current_plan_id`),
  ADD KEY `idx_subscription_status` (`subscription_status`);

--
-- Indexes for table `landlord_payouts`
--
ALTER TABLE `landlord_payouts`
  ADD PRIMARY KEY (`payout_id`),
  ADD UNIQUE KEY `uk_landlord_payouts_momo_request_id` (`momo_request_id`),
  ADD KEY `idx_landlord_payouts_landlord` (`landlord_id`),
  ADD KEY `idx_landlord_payouts_landlord_created` (`landlord_id`,`created_at`);

--
-- Indexes for table `landlord_subscriptions`
--
ALTER TABLE `landlord_subscriptions`
  ADD PRIMARY KEY (`subscription_id`),
  ADD KEY `plan_id` (`plan_id`),
  ADD KEY `last_payment_id` (`last_payment_id`),
  ADD KEY `idx_landlord_status` (`landlord_id`,`status`),
  ADD KEY `idx_end_date` (`end_date`);

--
-- Indexes for table `landlord_wallets`
--
ALTER TABLE `landlord_wallets`
  ADD PRIMARY KEY (`landlord_id`);

--
-- Indexes for table `momo_transactions`
--
ALTER TABLE `momo_transactions`
  ADD PRIMARY KEY (`id`),
  ADD KEY `ix_momo_transactions_request_id` (`request_id`),
  ADD KEY `ix_momo_transactions_payment_id` (`payment_id`);

--
-- Indexes for table `nearby_attractions`
--
ALTER TABLE `nearby_attractions`
  ADD PRIMARY KEY (`attraction_id`),
  ADD KEY `idx_location` (`location`);

--
-- Indexes for table `notifications`
--
ALTER TABLE `notifications`
  ADD PRIMARY KEY (`notification_id`),
  ADD KEY `idx_user_read` (`user_id`,`is_read`),
  ADD KEY `idx_user_created` (`user_id`,`created_at`),
  ADD KEY `idx_type` (`type`),
  ADD KEY `idx_reference` (`reference_type`,`reference_id`);

--
-- Indexes for table `packages`
--
ALTER TABLE `packages`
  ADD PRIMARY KEY (`package_id`),
  ADD KEY `idx_apartment` (`apartment_id`),
  ADD KEY `idx_active` (`is_active`);

--
-- Indexes for table `package_items`
--
ALTER TABLE `package_items`
  ADD PRIMARY KEY (`package_item_id`);

--
-- Indexes for table `package_packages`
--
ALTER TABLE `package_packages`
  ADD PRIMARY KEY (`id`),
  ADD KEY `package_packages_ibfk_1` (`package_id`),
  ADD KEY `package_packages_ibfk_2` (`package_item_id`);

--
-- Indexes for table `payments`
--
ALTER TABLE `payments`
  ADD PRIMARY KEY (`payment_id`),
  ADD KEY `idx_entity` (`related_entity_type`,`related_entity_id`),
  ADD KEY `idx_purpose` (`payment_purpose`),
  ADD KEY `idx_status` (`status`),
  ADD KEY `idx_landlord_id` (`landlord_id`);

--
-- Indexes for table `property_inspections`
--
ALTER TABLE `property_inspections`
  ADD PRIMARY KEY (`inspection_id`),
  ADD KEY `approved_by` (`approved_by`),
  ADD KEY `idx_apartment` (`apartment_id`),
  ADD KEY `idx_status` (`status`),
  ADD KEY `idx_inspector` (`inspector_id`);

--
-- Indexes for table `report_definitions`
--
ALTER TABLE `report_definitions`
  ADD PRIMARY KEY (`report_id`),
  ADD KEY `idx_report_definitions_category_type_active` (`category`,`type`,`is_active`);

--
-- Indexes for table `report_query_configs`
--
ALTER TABLE `report_query_configs`
  ADD PRIMARY KEY (`report_id`);

--
-- Indexes for table `reviews`
--
ALTER TABLE `reviews`
  ADD PRIMARY KEY (`review_id`),
  ADD UNIQUE KEY `uk_booking_review` (`booking_id`,`reviewer_id`),
  ADD KEY `reviewer_id` (`reviewer_id`),
  ADD KEY `reviewed_id` (`reviewed_id`),
  ADD KEY `apartment_id` (`apartment_id`);

--
-- Indexes for table `rooms`
--
ALTER TABLE `rooms`
  ADD PRIMARY KEY (`room_id`),
  ADD UNIQUE KEY `uk_room_in_apartment` (`apartment_id`),
  ADD KEY `idx_apartment` (`apartment_id`);

--
-- Indexes for table `scheduled_reports`
--
ALTER TABLE `scheduled_reports`
  ADD PRIMARY KEY (`scheduled_report_id`),
  ADD KEY `idx_scheduled_reports_report_id` (`report_id`),
  ADD KEY `idx_scheduled_reports_next_run_at` (`next_run_at`);

--
-- Indexes for table `smart_pricing_history`
--
ALTER TABLE `smart_pricing_history`
  ADD PRIMARY KEY (`pricing_id`),
  ADD KEY `idx_apartment_date` (`apartment_id`,`date`);

--
-- Indexes for table `subscription_plans`
--
ALTER TABLE `subscription_plans`
  ADD PRIMARY KEY (`plan_id`),
  ADD KEY `idx_active` (`is_active`);

--
-- Indexes for table `support_tickets`
--
ALTER TABLE `support_tickets`
  ADD PRIMARY KEY (`ticket_id`),
  ADD KEY `resolved_by` (`resolved_by`),
  ADD KEY `idx_status` (`status`),
  ADD KEY `idx_priority` (`priority`),
  ADD KEY `idx_category` (`category`),
  ADD KEY `idx_user` (`user_id`);

--
-- Indexes for table `support_ticket_assignments`
--
ALTER TABLE `support_ticket_assignments`
  ADD PRIMARY KEY (`assignment_id`),
  ADD UNIQUE KEY `uk_assignment` (`ticket_id`,`staff_id`),
  ADD KEY `staff_id` (`staff_id`);

--
-- Indexes for table `temporary_residence_reports`
--
ALTER TABLE `temporary_residence_reports`
  ADD PRIMARY KEY (`report_id`),
  ADD UNIQUE KEY `uk_booking` (`booking_id`),
  ADD KEY `landlord_id` (`landlord_id`);

--
-- Indexes for table `tenants`
--
ALTER TABLE `tenants`
  ADD PRIMARY KEY (`tenant_id`),
  ADD KEY `idx_verification_status` (`identity_verification_status`);

--
-- Indexes for table `tenant_wishlists`
--
ALTER TABLE `tenant_wishlists`
  ADD PRIMARY KEY (`wishlist_id`),
  ADD UNIQUE KEY `uk_collection_apartment` (`collection_id`,`apartment_id`),
  ADD KEY `tenant_wishlists_ibfk_2` (`apartment_id`),
  ADD KEY `idx_tenant_id` (`tenant_id`),
  ADD KEY `idx_collection_id` (`collection_id`);

--
-- Indexes for table `users`
--
ALTER TABLE `users`
  ADD PRIMARY KEY (`user_id`),
  ADD UNIQUE KEY `email` (`email`),
  ADD KEY `idx_role` (`role`),
  ADD KEY `idx_email` (`email`);

--
-- Indexes for table `user_identity_documents`
--
ALTER TABLE `user_identity_documents`
  ADD PRIMARY KEY (`document_id`),
  ADD KEY `idx_user` (`user_id`),
  ADD KEY `idx_status` (`verification_status`),
  ADD KEY `idx_type` (`document_type`);

--
-- Indexes for table `identity_document_ocr_results`
--
ALTER TABLE `identity_document_ocr_results`
  ADD PRIMARY KEY (`ocr_result_id`),
  ADD KEY `idx_document` (`document_id`),
  ADD KEY `idx_provider` (`provider`),
  ADD KEY `idx_processed_at` (`processed_at`);

--
-- Indexes for table `wishlist_collections`
--
ALTER TABLE `wishlist_collections`
  ADD PRIMARY KEY (`collection_id`),
  ADD UNIQUE KEY `uk_wishlist_collections_tenant_name` (`tenant_id`,`name`),
  ADD KEY `idx_wishlist_collections_tenant_id` (`tenant_id`);

--
-- Indexes for table `__efmigrationshistory`
--
ALTER TABLE `__efmigrationshistory`
  ADD PRIMARY KEY (`MigrationId`);

--
-- AUTO_INCREMENT for dumped tables
--

--
-- AUTO_INCREMENT for table `momo_transactions`
--
ALTER TABLE `momo_transactions`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT;

--
-- Constraints for dumped tables
--

--
-- Constraints for table `admin_actions`
--
ALTER TABLE `admin_actions`
  ADD CONSTRAINT `admin_actions_ibfk_1` FOREIGN KEY (`admin_id`) REFERENCES `users` (`user_id`) ON DELETE CASCADE;

--
-- Constraints for table `apartments`
--
ALTER TABLE `apartments`
  ADD CONSTRAINT `apartments_ibfk_1` FOREIGN KEY (`landlord_id`) REFERENCES `landlords` (`landlord_id`) ON DELETE CASCADE;

--
-- Constraints for table `apartment_amenities`
--
ALTER TABLE `apartment_amenities`
  ADD CONSTRAINT `apartment_amenities_ibfk_1` FOREIGN KEY (`apartment_id`) REFERENCES `apartments` (`apartment_id`) ON DELETE CASCADE,
  ADD CONSTRAINT `apartment_amenities_ibfk_2` FOREIGN KEY (`amenity_id`) REFERENCES `amenities` (`amenity_id`) ON DELETE CASCADE;

--
-- Constraints for table `apartment_availability`
--
ALTER TABLE `apartment_availability`
  ADD CONSTRAINT `apartment_availability_ibfk_1` FOREIGN KEY (`apartment_id`) REFERENCES `apartments` (`apartment_id`) ON DELETE CASCADE;

--
-- Constraints for table `apartment_media`
--
ALTER TABLE `apartment_media`
  ADD CONSTRAINT `apartment_media_ibfk_1` FOREIGN KEY (`apartment_id`) REFERENCES `apartments` (`apartment_id`) ON DELETE CASCADE;

--
-- Constraints for table `apartment_price_calendar`
--
ALTER TABLE `apartment_price_calendar`
  ADD CONSTRAINT `apartment_price_calendar_ibfk_1` FOREIGN KEY (`apartment_id`) REFERENCES `apartments` (`apartment_id`) ON DELETE CASCADE;

--
-- Constraints for table `bookings`
--
ALTER TABLE `bookings`
  ADD CONSTRAINT `bookings_ibfk_1` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`tenant_id`) ON DELETE CASCADE,
  ADD CONSTRAINT `bookings_ibfk_2` FOREIGN KEY (`apartment_id`) REFERENCES `apartments` (`apartment_id`) ON DELETE CASCADE,
  ADD CONSTRAINT `bookings_ibfk_3` FOREIGN KEY (`package_id`) REFERENCES `packages` (`package_id`) ON DELETE SET NULL;

--
-- Constraints for table `booking_check_times`
--
ALTER TABLE `booking_check_times`
  ADD CONSTRAINT `booking_check_times_ibfk_1` FOREIGN KEY (`booking_id`) REFERENCES `bookings` (`booking_id`) ON DELETE CASCADE;

--
-- Constraints for table `booking_occupants`
--
ALTER TABLE `booking_occupants`
  ADD CONSTRAINT `booking_occupants_ibfk_1` FOREIGN KEY (`booking_id`) REFERENCES `bookings` (`booking_id`) ON DELETE CASCADE;

--
-- Constraints for table `booking_offers`
--
ALTER TABLE `booking_offers`
  ADD CONSTRAINT `booking_offers_ibfk_1` FOREIGN KEY (`original_booking_id`) REFERENCES `bookings` (`booking_id`),
  ADD CONSTRAINT `booking_offers_ibfk_2` FOREIGN KEY (`alternative_apartment_id`) REFERENCES `apartments` (`apartment_id`),
  ADD CONSTRAINT `booking_offers_ibfk_3` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`tenant_id`),
  ADD CONSTRAINT `booking_offers_ibfk_4` FOREIGN KEY (`created_by_staff_id`) REFERENCES `users` (`user_id`) ON DELETE SET NULL;

--
-- Constraints for table `generated_reports`
--
ALTER TABLE `generated_reports`
  ADD CONSTRAINT `fk_generated_reports_report_definitions_report_id` FOREIGN KEY (`report_id`) REFERENCES `report_definitions` (`report_id`) ON DELETE CASCADE;

--
-- Constraints for table `inspection_photos`
--
ALTER TABLE `inspection_photos`
  ADD CONSTRAINT `inspection_photos_ibfk_1` FOREIGN KEY (`inspection_id`) REFERENCES `property_inspections` (`inspection_id`) ON DELETE CASCADE;

--
-- Constraints for table `landlords`
--
ALTER TABLE `landlords`
  ADD CONSTRAINT `landlords_ibfk_1` FOREIGN KEY (`landlord_id`) REFERENCES `users` (`user_id`) ON DELETE CASCADE,
  ADD CONSTRAINT `landlords_ibfk_2` FOREIGN KEY (`current_plan_id`) REFERENCES `subscription_plans` (`plan_id`) ON DELETE SET NULL;

--
-- Constraints for table `landlord_payouts`
--
ALTER TABLE `landlord_payouts`
  ADD CONSTRAINT `fk_landlord_payouts_landlord` FOREIGN KEY (`landlord_id`) REFERENCES `landlords` (`landlord_id`) ON DELETE CASCADE;

--
-- Constraints for table `landlord_subscriptions`
--
ALTER TABLE `landlord_subscriptions`
  ADD CONSTRAINT `landlord_subscriptions_ibfk_1` FOREIGN KEY (`landlord_id`) REFERENCES `landlords` (`landlord_id`) ON DELETE CASCADE,
  ADD CONSTRAINT `landlord_subscriptions_ibfk_2` FOREIGN KEY (`plan_id`) REFERENCES `subscription_plans` (`plan_id`),
  ADD CONSTRAINT `landlord_subscriptions_ibfk_3` FOREIGN KEY (`last_payment_id`) REFERENCES `payments` (`payment_id`) ON DELETE SET NULL;

--
-- Constraints for table `landlord_wallets`
--
ALTER TABLE `landlord_wallets`
  ADD CONSTRAINT `landlord_wallets_ibfk_1` FOREIGN KEY (`landlord_id`) REFERENCES `landlords` (`landlord_id`);

--
-- Constraints for table `momo_transactions`
--
ALTER TABLE `momo_transactions`
  ADD CONSTRAINT `fk_momo_transactions_payment` FOREIGN KEY (`payment_id`) REFERENCES `payments` (`payment_id`);

--
-- Constraints for table `notifications`
--
ALTER TABLE `notifications`
  ADD CONSTRAINT `notifications_ibfk_1` FOREIGN KEY (`user_id`) REFERENCES `users` (`user_id`) ON DELETE CASCADE;

--
-- Constraints for table `packages`
--
ALTER TABLE `packages`
  ADD CONSTRAINT `packages_ibfk_1` FOREIGN KEY (`apartment_id`) REFERENCES `apartments` (`apartment_id`) ON DELETE CASCADE;

--
-- Constraints for table `package_packages`
--
ALTER TABLE `package_packages`
  ADD CONSTRAINT `package_packages_ibfk_1` FOREIGN KEY (`package_id`) REFERENCES `packages` (`package_id`),
  ADD CONSTRAINT `package_packages_ibfk_2` FOREIGN KEY (`package_item_id`) REFERENCES `package_items` (`package_item_id`);

--
-- Constraints for table `property_inspections`
--
ALTER TABLE `property_inspections`
  ADD CONSTRAINT `property_inspections_ibfk_1` FOREIGN KEY (`apartment_id`) REFERENCES `apartments` (`apartment_id`) ON DELETE CASCADE,
  ADD CONSTRAINT `property_inspections_ibfk_2` FOREIGN KEY (`inspector_id`) REFERENCES `users` (`user_id`) ON DELETE CASCADE,
  ADD CONSTRAINT `property_inspections_ibfk_3` FOREIGN KEY (`approved_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL;

--
-- Constraints for table `report_query_configs`
--
ALTER TABLE `report_query_configs`
  ADD CONSTRAINT `fk_report_query_configs_report_definitions_report_id` FOREIGN KEY (`report_id`) REFERENCES `report_definitions` (`report_id`) ON DELETE CASCADE;

--
-- Constraints for table `reviews`
--
ALTER TABLE `reviews`
  ADD CONSTRAINT `reviews_ibfk_1` FOREIGN KEY (`booking_id`) REFERENCES `bookings` (`booking_id`) ON DELETE CASCADE,
  ADD CONSTRAINT `reviews_ibfk_2` FOREIGN KEY (`reviewer_id`) REFERENCES `users` (`user_id`) ON DELETE CASCADE,
  ADD CONSTRAINT `reviews_ibfk_3` FOREIGN KEY (`reviewed_id`) REFERENCES `users` (`user_id`) ON DELETE CASCADE,
  ADD CONSTRAINT `reviews_ibfk_4` FOREIGN KEY (`apartment_id`) REFERENCES `apartments` (`apartment_id`) ON DELETE SET NULL;

--
-- Constraints for table `rooms`
--
ALTER TABLE `rooms`
  ADD CONSTRAINT `rooms_ibfk_1` FOREIGN KEY (`apartment_id`) REFERENCES `apartments` (`apartment_id`) ON DELETE CASCADE;

--
-- Constraints for table `scheduled_reports`
--
ALTER TABLE `scheduled_reports`
  ADD CONSTRAINT `fk_scheduled_reports_report_definitions_report_id` FOREIGN KEY (`report_id`) REFERENCES `report_definitions` (`report_id`) ON DELETE CASCADE;

--
-- Constraints for table `smart_pricing_history`
--
ALTER TABLE `smart_pricing_history`
  ADD CONSTRAINT `smart_pricing_history_ibfk_1` FOREIGN KEY (`apartment_id`) REFERENCES `apartments` (`apartment_id`) ON DELETE CASCADE;

--
-- Constraints for table `support_tickets`
--
ALTER TABLE `support_tickets`
  ADD CONSTRAINT `support_tickets_ibfk_1` FOREIGN KEY (`user_id`) REFERENCES `users` (`user_id`) ON DELETE CASCADE,
  ADD CONSTRAINT `support_tickets_ibfk_2` FOREIGN KEY (`resolved_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL;

--
-- Constraints for table `support_ticket_assignments`
--
ALTER TABLE `support_ticket_assignments`
  ADD CONSTRAINT `support_ticket_assignments_ibfk_1` FOREIGN KEY (`ticket_id`) REFERENCES `support_tickets` (`ticket_id`) ON DELETE CASCADE,
  ADD CONSTRAINT `support_ticket_assignments_ibfk_2` FOREIGN KEY (`staff_id`) REFERENCES `users` (`user_id`) ON DELETE CASCADE;

--
-- Constraints for table `temporary_residence_reports`
--
ALTER TABLE `temporary_residence_reports`
  ADD CONSTRAINT `temporary_residence_reports_ibfk_1` FOREIGN KEY (`booking_id`) REFERENCES `bookings` (`booking_id`) ON DELETE CASCADE,
  ADD CONSTRAINT `temporary_residence_reports_ibfk_2` FOREIGN KEY (`landlord_id`) REFERENCES `landlords` (`landlord_id`) ON DELETE CASCADE;

--
-- Constraints for table `tenants`
--
ALTER TABLE `tenants`
  ADD CONSTRAINT `tenants_ibfk_1` FOREIGN KEY (`tenant_id`) REFERENCES `users` (`user_id`) ON DELETE CASCADE;

--
-- Constraints for table `tenant_wishlists`
--
ALTER TABLE `tenant_wishlists`
  ADD CONSTRAINT `tenant_wishlists_ibfk_1` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`tenant_id`) ON DELETE CASCADE,
  ADD CONSTRAINT `tenant_wishlists_ibfk_2` FOREIGN KEY (`apartment_id`) REFERENCES `apartments` (`apartment_id`) ON DELETE CASCADE,
  ADD CONSTRAINT `tenant_wishlists_ibfk_3` FOREIGN KEY (`collection_id`) REFERENCES `wishlist_collections` (`collection_id`) ON DELETE CASCADE;

--
-- Constraints for table `user_identity_documents`
--
ALTER TABLE `user_identity_documents`
  ADD CONSTRAINT `user_identity_documents_ibfk_1` FOREIGN KEY (`user_id`) REFERENCES `users` (`user_id`) ON DELETE CASCADE;

--
-- Constraints for table `identity_document_ocr_results`
--
ALTER TABLE `identity_document_ocr_results`
  ADD CONSTRAINT `identity_document_ocr_results_ibfk_1` FOREIGN KEY (`document_id`) REFERENCES `user_identity_documents` (`document_id`) ON DELETE CASCADE;

--
-- Constraints for table `wishlist_collections`
--
ALTER TABLE `wishlist_collections`
  ADD CONSTRAINT `wishlist_collections_ibfk_1` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`tenant_id`) ON DELETE CASCADE;
COMMIT;

/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
