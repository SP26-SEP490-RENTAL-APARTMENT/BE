ALTER TABLE tenants
ADD COLUMN sex VARCHAR(20) NULL AFTER passport_id,
ADD COLUMN birthday DATE NULL AFTER sex,
ADD COLUMN national_id_card_number VARCHAR(12) NULL AFTER nationality,
ADD CONSTRAINT chk_tenants_national_id_card_number
    CHECK (national_id_card_number IS NULL OR national_id_card_number REGEXP '^0[0-9]{11}$');
