-- Move tenant profile identity fields to users table.
-- Columns moved: sex, birthday, nationality, national_id_card_number
SET SQL_SAFE_UPDATES = 0;

ALTER TABLE users
    ADD COLUMN sex VARCHAR(20) NULL,
    ADD COLUMN birthday DATE NULL,
    ADD COLUMN nationality CHAR(2) NULL COMMENT 'ISO 3166-1 alpha-2 code (e.g. VN, US, KR). Used for temp residence reporting',
    ADD COLUMN national_id_card_number VARCHAR(12) NULL;

-- Backfill users from tenants where values are currently stored.
UPDATE users u
JOIN tenants t ON t.tenant_id = u.user_id
SET
    u.sex = COALESCE(u.sex, t.sex),
    u.birthday = COALESCE(u.birthday, t.birthday),
    u.nationality = COALESCE(u.nationality, t.nationality),
    u.national_id_card_number = COALESCE(u.national_id_card_number, t.national_id_card_number)
WHERE
    t.sex IS NOT NULL
    OR t.birthday IS NOT NULL
    OR t.nationality IS NOT NULL
    OR t.national_id_card_number IS NOT NULL;

-- Remove moved columns from tenants.
ALTER TABLE tenants
    DROP COLUMN sex,
    DROP COLUMN birthday,
    DROP COLUMN nationality,
    DROP COLUMN national_id_card_number;

SET SQL_SAFE_UPDATES = 1;