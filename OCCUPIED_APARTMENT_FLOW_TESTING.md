# Occupied Apartment Flow for Testing

## Purpose
This file describes the occupied-apartment flow as a testable sequence. Use it when you want to verify the incident-report path, alternative offer lifecycle, tenant response handling, and penalty follow-up.

## What Triggers The Flow
The flow starts when a tenant arrives at the booked apartment and finds that the room is occupied or otherwise unavailable.

The system then moves through these stages:
1. Tenant reports the issue.
2. The system creates a support ticket.
3. The system searches for alternative apartments.
4. Staff confirms the incident.
5. The tenant is refunded first.
6. The system publishes an alternative offer afterward.
7. The tenant views and responds to the offer.
8. Staff can confirm the incident penalty after settlement.

## Main Endpoints
- GET /api/booking/{id}/occupied-alternatives
- POST /api/booking/{id}/occupied-incident
- POST /api/booking/{id}/occupied-offers
- GET /api/booking/occupied-offers/my
- POST /api/booking/occupied-offers/{offerId}/respond
- POST /api/booking/{id}/occupied-incident/confirm-penalty

## Flow Details

### 1. Tenant Reports The Incident
The tenant sends a report with a short description of the occupied-apartment problem.

Expected behavior:
- The request must be made by the tenant who owns the booking.
- A support ticket is created for tracking.
- The report succeeds without creating an immediate alternative offer.
- Staff will handle refund and offer publication after confirming the incident.

### 2. System Finds Alternative Apartments
The service looks for apartments that can replace the original booking.

Validation rules:
- Same city as the original apartment.
- Same district as the original apartment.
- Must be posted and available.
- Must satisfy occupancy requirements.
- Must satisfy pet requirements when the booking includes pets.
- Must not conflict with existing bookings.
- Must stay within the configured price tolerance.

### 3. Staff Or System Creates An Offer
An alternative offer can be created in two ways:
- Automatically after occupied-incident confirmation, after the refund is processed.
- Manually by staff or admin.

Expected behavior:
- The offer status starts as pending.
- The offer stores the original booking, the alternative apartment, and the tenant.
- A response expiry time is set.
- Notifications are sent to the tenant and the original apartment landlord.

### 4. Tenant Reviews Active Offers
The tenant requests active offers using the my endpoint.

Expected behavior:
- Only pending offers are returned.
- Expired offers are excluded from the list.
- Each response includes the alternative apartment details and pricing difference.

### 5. Tenant Responds To An Offer
The tenant accepts or rejects one active offer.

Expected behavior when accepted:
- The selected offer becomes accepted.
- Any sibling pending offers for the same booking become cancelled.
- The tenant receives an acceptance notification.
- Manual settlement is still required after acceptance.

Expected behavior when rejected:
- The offer becomes rejected.
- The tenant receives a rejection notification.

Expected behavior when expired:
- The offer is marked expired.
- The response is rejected with an error.

### 6. Staff Confirms The Penalty
After the incident is handled, staff can confirm the penalty settlement.

Expected behavior:
- The tenant refund is processed first if it has not already been applied.
- An alternative offer is published after refunding.
- The endpoint returns settlement details.
- The booking incident is treated as closed from the operational side.

## Test Scenarios

### Happy Path
1. Create or identify a booking that can be used as the occupied booking.
2. Call POST /api/booking/{id}/occupied-incident as the tenant.
3. Confirm the support ticket is created.
4. Call POST /api/booking/{id}/occupied-incident/confirm-penalty as staff or admin.
5. Confirm the refund is processed before the offer appears.
6. Call GET /api/booking/occupied-offers/my as the tenant.
7. Accept one offer.
8. Confirm sibling offers are cancelled.
9. Confirm the penalty through the staff endpoint.

### Manual Offer Path
1. Call GET /api/booking/{id}/occupied-alternatives.
2. Choose one valid apartment.
3. Call POST /api/booking/{id}/occupied-offers as staff or admin.
4. Confirm the offer is pending.
5. Log in as the tenant and confirm the offer appears in active offers.
6. Respond to the offer.

### Negative Path
- Try to create an offer with the original apartment as the alternative.
- Try to create an offer for an apartment in a different city or district.
- Try to respond to an offer that belongs to another tenant.
- Try to respond to a non-pending offer.
- Try to respond after expiry.

## What To Verify In Tests
- HTTP status codes are correct for success and failure cases.
- Offer status changes are persisted in the database.
- Sibling offers are cancelled after acceptance.
- Notifications are generated for the tenant and landlord.
- The response payload contains the correct apartment and price information.
- Manual settlement is still required after acceptance.

## Suggested Evidence
- Request and response bodies.
- Offer status before and after response.
- Support ticket id.
- Notification records if available.
- Penalty confirmation response.

## Notes
- The flow uses UTC-based expiry checks.
- Active offers are filtered by pending status and expiry time.
- This document is intended for manual testing and for writing API integration tests.