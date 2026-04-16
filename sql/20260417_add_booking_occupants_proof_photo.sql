ALTER TABLE booking_occupants
ADD COLUMN IF NOT EXISTS proof_photo_url VARCHAR(1000) NULL;