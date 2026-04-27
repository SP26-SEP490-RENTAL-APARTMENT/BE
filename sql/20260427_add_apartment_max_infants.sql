-- Add max_infants capacity for apartments
ALTER TABLE apartments
  ADD COLUMN max_infants TINYINT NULL AFTER max_occupants;
