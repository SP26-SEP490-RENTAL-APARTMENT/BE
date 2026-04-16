-- Add per-occupant date of birth for booking occupants.
ALTER TABLE booking_occupants
ADD COLUMN date_of_birth DATE NULL AFTER passport_id;
