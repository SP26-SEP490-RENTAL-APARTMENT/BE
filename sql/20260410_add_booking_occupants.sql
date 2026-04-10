CREATE TABLE IF NOT EXISTS booking_occupants (
    occupant_id CHAR(36) NOT NULL,
    booking_id CHAR(36) NOT NULL,
    occupant_order INT NOT NULL,
    is_primary TINYINT(1) NOT NULL DEFAULT 0,
    full_name VARCHAR(150) NULL,
    passport_id VARCHAR(50) NULL,
    national_id_card_number VARCHAR(20) NULL,
    nationality CHAR(2) NULL,
    sex VARCHAR(20) NULL,
    phone VARCHAR(20) NULL,
    email VARCHAR(255) NULL,
    created_at TIMESTAMP NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (occupant_id),
    KEY idx_booking (booking_id),
    UNIQUE KEY uk_booking_order (booking_id, occupant_order),
    KEY idx_booking_primary (booking_id, is_primary),
    CONSTRAINT booking_occupants_ibfk_1 FOREIGN KEY (booking_id) REFERENCES bookings (booking_id) ON DELETE CASCADE
);

INSERT INTO booking_occupants (
    occupant_id,
    booking_id,
    occupant_order,
    is_primary,
    full_name,
    passport_id,
    national_id_card_number,
    nationality,
    sex,
    phone,
    email
)
SELECT
    UUID(),
    b.booking_id,
    1,
    1,
    u.full_name,
    tr.tenant_passport_id,
    u.national_id_card_number,
    tr.tenant_nationality,
    u.sex,
    u.phone,
    u.email
FROM bookings b
INNER JOIN users u ON u.user_id = b.tenant_id
LEFT JOIN temporary_residence_reports tr ON tr.booking_id = b.booking_id
LEFT JOIN booking_occupants bo ON bo.booking_id = b.booking_id AND bo.occupant_order = 1
WHERE bo.occupant_id IS NULL;