-- Add children count to bookings while keeping infant count
ALTER TABLE bookings
  ADD COLUMN noOfChildren INT NULL AFTER noOfAdults;
