# Occupied Apartment Flow for Testing

## Purpose
This file describes the occupied-apartment flow as a testable sequence. Use it when you want to verify the incident-report path, alternative offer lifecycle, tenant response handling, and penalty follow-up.

## What Triggers The Flow
The flow starts when a tenant arrives at the booked apartment and finds that the room is occupied or otherwise unavailable.

The system then moves through these stages:
1. Tenant reports the issue.
2. The system creates a support ticket **and immediately searches for alternatives** (concurrent investigation).
3. The system publishes the first viable alternative offer right away.
4. The tenant views and responds to the offer (acceptance or rejection).
5. If rejected, the system automatically searches for and publishes the next viable alternative.
6. Staff later confirms the incident and applies settlement (refund + penalty).

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
- **The system immediately searches for and publishes an alternative offer** (no waiting for staff confirmation).
- The tenant can review alternatives right away while the system investigates.

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
- Automatically after the tenant reports the incident (no waiting for staff confirmation).
- Manually by staff or admin if the automatic search doesn't produce viable alternatives.

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

### 5. Tenant Responds To An Offer (Accept or Reject)
The tenant accepts or rejects one active offer.

Expected behavior when accepted:
- The selected offer becomes accepted.
- Any sibling pending offers for the same booking become cancelled.
- The tenant receives an acceptance notification.
- Staff will later complete settlement (refund + penalty confirmation).

Expected behavior when rejected:
- The offer becomes rejected.
- **The system automatically searches for and publishes the next viable alternative** (if available).
- The tenant receives a rejection notification (and potentially a new offer notification if alternatives were found).

Expected behavior when expired:
- The offer is marked expired.
- The response is rejected with an error.

### 6. Staff Confirms The Incident (Settlement)
After the tenant has accepted an alternative, staff reviews the support ticket and confirms the incident.

Expected behavior:
- Staff marks the incident as confirmed.
- The system automatically refunds the original booking (if not already refunded).
- An alternative offer is published if none exist yet (for manual ticket paths).
- The system applies a penalty to the landlord's wallet.
- The endpoint returns settlement details.
- The booking incident is treated as closed from the operational side.

## Test Scenarios

### Happy Path (Immediate Alternative + Accept)
1. Create or identify a booking that can be used as the occupied booking.
2. Call POST /api/booking/{id}/occupied-incident as the tenant.
3. Confirm the support ticket is created AND an alternative offer is published immediately.
4. Call GET /api/booking/occupied-offers/my as the tenant.
5. Confirm the alternative apartment is available and details are accurate.
6. Call POST /api/booking/occupied-offers/{offerId}/respond with accepted=true as the tenant.
7. Confirm the selected offer becomes accepted.
8. Confirm sibling offers are cancelled.
9. Call POST /api/booking/{id}/occupied-incident/confirm-penalty as staff or admin.
10. Confirm the refund is processed.
11. Confirm the penalty is applied to the landlord.

### Alternative Rejection With Auto-Recovery Path
1. Execute steps 1-5 of the Happy Path.
2. Call POST /api/booking/occupied-offers/{offerId}/respond with accepted=false as the tenant.
3. Confirm the offer becomes rejected.
4. **Confirm a new alternative offer is automatically published** (no staff action needed).
5. Call GET /api/booking/occupied-offers/my as the tenant to see the new offer.
6. Accept the new offer and proceed to step 9 of the Happy Path.

### Manual Offer Path (Fallback)
1. Call GET /api/booking/{id}/occupied-alternatives as staff or admin.
2. Choose one valid apartment.
3. Call POST /api/booking/{id}/occupied-offers as staff or admin to manually create an offer.
4. Confirm the offer is pending.
5. Call POST /api/booking/occupied-offers/{offerId}/respond as the tenant to accept it.

### No Viable Alternatives Path
1. Create a booking in a remote location where no alternatives exist.
2. Call POST /api/booking/{id}/occupied-incident as the tenant.
3. Confirm the support ticket is created.
4. Confirm that GET /api/booking/occupied-offers/my shows no pending offers (search had no results).
5. Staff manually creates an alternative using POST /api/booking/{id}/occupied-offers.
6. Tenant accepts the manual offer.

### Negative Path
- Try to create an offer with the original apartment as the alternative.
- Try to create an offer for an apartment in a different city or district.
- Try to respond to an offer that belongs to another tenant.
- Try to respond to a non-pending offer.
- Try to respond after expiry.
- Try to respond to a booking that doesn't belong to the current user.

## What To Verify In Tests
- HTTP status codes are correct for success and failure cases.
- Offer status changes are persisted in the database.
- Sibling offers are cancelled after acceptance.
- New alternatives are automatically published after rejection.
- Notifications are generated for the tenant and landlord.
- The response payload contains the correct apartment and price information.
- Refund happens AFTER tenant accepts an alternative (not before offer publication).
- Penalty is applied to the landlord after staff confirms the incident.

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