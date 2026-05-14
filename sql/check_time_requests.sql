-- Check if table doesn't already exist
DROP TABLE IF EXISTS check_time_requests;

-- Temporarily disable FK checks for clean create
SET FOREIGN_KEY_CHECKS=0;

CREATE TABLE check_time_requests (
    -- Primary Key (UUID)
    check_time_request_id CHAR(36) NOT NULL DEFAULT (UUID()) PRIMARY KEY,

    `booking_id` CHAR(36) NOT NULL,

    -- 'EarlyCheckIn' or 'LateCheckOut'
    `request_type` VARCHAR(50) NOT NULL,

    -- Guest's original ask (immutable)
    `requested_time` DATETIME(6) NOT NULL,

    -- Host's counter-offer
    `counter_offered_time` DATETIME(6) NULL,
    `counter_offered_fee` DECIMAL(18,2) NULL,

    -- Final agreed time & fee
    `agreed_time` DATETIME(6) NULL,
    `agreed_fee` DECIMAL(18,2) NULL,

    -- Status lifecycle
    `request_status` ENUM('Pending', 'CounterOffered', 'Approved', 'Rejected', 'Expired') DEFAULT 'Pending',

    `guest_reason` VARCHAR(500) NULL,
    `host_response` VARCHAR(500) NULL,

    -- Process tracking (use UTC but store as-is)
    `created_at` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `expires_at` DATETIME(6) NULL,
    `processed_at` DATETIME(6) NULL,
    `processed_by_id` CHAR(36) NULL,
    
    -- Payment & operational
    `payment_reference_id` VARCHAR(255) NULL,
    `time_zone_id` VARCHAR(64) NULL DEFAULT 'Asia/Ho_Chi_Minh',
    `cleaning_notes_for_crew` TEXT NULL,
    `fee_paid_or_waived` TINYINT(1) DEFAULT 0,

    -- Soft delete
    `is_deleted` TINYINT(1) DEFAULT 0,
    `deleted_at` DATETIME(6) NULL,

    -- Foreign key - MUST use backticks for table/column names
    CONSTRAINT `FK_CheckTimeRequests_Bookings` 
        FOREIGN KEY (`booking_id`) 
        REFERENCES `bookings`(`booking_id`)
        ON DELETE CASCADE 
        ON UPDATE CASCADE,

    -- Indexes
    INDEX `IX_BookingId_Status` (`booking_id`, `request_status`),
    INDEX `IX_ExpiresAt` (`expires_at`),
    INDEX `IX_CreatedAt` (`created_at`),

    -- Unique active request per booking + type
    UNIQUE KEY `UX_ActiveRequest` (
        `booking_id`, 
        `request_type`, 
        `request_status`
    )

) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- Re-enable FK checks
SET FOREIGN_KEY_CHECKS=1;

-- Verify table created
SELECT TABLE_NAME, TABLE_TYPE FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'check_time_requests';