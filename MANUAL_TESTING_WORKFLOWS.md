# Manual Testing Workflows

## Purpose
This document provides an executable manual API testing workflow for the Short-termApartmentAPI backend.
It is organized by role and dependency order so QA can run from setup to business-critical verification.

## Global Prerequisites
- API is running locally.
- Test environment has seeded or created users for: admin, staff, landlord, tenant.
- You can call APIs using Postman, Swagger, or another HTTP client.
- You can persist artifacts between steps: access token, refresh token, user ids, apartment id, booking id, payment ids.

## Required Artifacts To Track During Testing
- AdminAccessToken
- StaffAccessToken
- LandlordAccessToken
- TenantAccessToken
- TenantUserId
- LandlordUserId
- ApartmentId
- BookingId
- StripeSessionId or MomoOrderId

## Response Checks For Every Endpoint
- Correct HTTP status code.
- Authorization and role enforcement.
- Ownership enforcement for user-scoped resources.
- Business state transition (if endpoint mutates status/state).
- Data visibility rules (forbidden data should be hidden or denied).

---

## Workflow 1: Authentication Bootstrap
Controller: Short-termApartmentAPI/Controllers/AuthController.cs

### Steps
1. Register a landlord user.
- Endpoint: POST /api/auth/register
- Save: landlord account credentials
- Expected: 200

2. Register a tenant user.
- Endpoint: POST /api/auth/register
- Save: tenant account credentials
- Expected: 200

3. Login with each role account.
- Endpoint: POST /api/auth/login
- Save: access token and refresh token
- Expected: 200

4. Refresh token flow.
- Endpoint: POST /api/auth/refresh-token
- Expected: 200 with new token pair

5. Logout flow.
- Endpoint: POST /api/auth/logout
- Expected: 200

### Negative Tests
- Login with invalid password should return 401.
- Refresh with invalid/expired token should return 401.

---

## Workflow 2: Identity Verification (Booking Prerequisite)
Controller: Short-termApartmentAPI/Controllers/IdentityVerificationController.cs

### Tenant Steps
1. Upload tenant identity document.
- Endpoint: POST /api/identity/documents
- Auth: tenant
- Expected: 200 and pending verification message

2. Retrieve tenant documents.
- Endpoint: GET /api/identity/documents
- Auth: tenant
- Expected: 200 with uploaded document

### Staff/Admin Review Steps
3. Review and approve tenant document.
- Endpoint: POST /api/identity/documents/review
- Auth: staff or admin
- Expected: 200

4. Verify tenant documents by user id.
- Endpoint: GET /api/identity/users/{userId}/documents
- Auth: staff or admin
- Expected: 200 and verified document status

### Negative Tests
- Tenant calls review endpoint should return 403.
- Missing/invalid token should return 401.

---

## Workflow 3: Listing Creation To Published State
Controllers:
- Short-termApartmentAPI/Controllers/ApartmentController.cs
- Short-termApartmentAPI/Controllers/AmenityController.cs
- Short-termApartmentAPI/Controllers/RoomController.cs

### Landlord Steps
1. Create apartment with photos.
- Endpoint: POST /api/apartments
- Auth: landlord
- Save: ApartmentId
- Expected: 201 and status draft

2. Add amenities to apartment.
- Endpoint: POST /api/apartments/{id}/amenities
- Auth: landlord
- Expected: 200

3. Create room for apartment.
- Endpoint: POST /api/rooms
- Auth: landlord
- Expected: 201

4. Submit listing for review.
- Endpoint: POST /api/apartments/{id}/submit-for-review
- Auth: landlord
- Expected: 200 and status pending_review

### Admin Step
5. Approve listing.
- Endpoint: POST /api/apartments/{id}/approve
- Auth: admin
- Expected: 200 and status posted

### Rejection Branch
- Use approved false with rejection reason.
- Expected: status blocked.

### Negative Tests
- Submit for review without required listing data should return 400.
- Non-owner landlord attempts update/delete should be denied (not found or forbidden behavior).
- Non-admin approve call should return 403.

---

## Workflow 4: Smart Pricing
Controller: Short-termApartmentAPI/Controllers/SmartPricingController.cs

### Steps
1. Generate suggestion for landlord-owned apartment.
- Endpoint: POST /api/smart-pricing/suggest
- Auth: landlord
- Input: apartmentId, date, optional occupancyRate
- Expected: 200 with pricingId and suggestedPrice

2. Accept suggestion.
- Endpoint: POST /api/smart-pricing/{pricingId}/accept
- Auth: landlord
- Expected: 200 and accepted response

3. Accept with override.
- Endpoint: POST /api/smart-pricing/{pricingId}/accept
- Auth: landlord
- Input: overridePrice
- Expected: 200

### Negative Tests
- Non-owner landlord call should return 404/permission denied message.
- Invalid occupancy rate should return 400.

---

## Workflow 5: Tenant Booking Core Flow
Controller: Short-termApartmentAPI/Controllers/BookingController.cs

### Steps
1. Get quote for posted apartment.
- Endpoint: POST /api/booking/quote
- Auth: tenant
- Input: apartmentId, checkInDate, checkOutDate
- Expected: 200 and quote breakdown

2. Create booking.
- Endpoint: POST /api/booking
- Auth: tenant
- Input: apartmentId, dates, paymentProvider optional
- Save: BookingId
- Expected: 201 and status pending

3. Retrieve booking details.
- Endpoint: GET /api/booking/{id}
- Auth: authenticated
- Expected: 200 for authorized access

### Critical Verification Requirement Test
4. Attempt create booking with unverified tenant account.
- Endpoint: POST /api/booking
- Auth: unverified tenant
- Expected: 400 due to identity verification rule

### Negative Tests
- Booking non-posted apartment should return 400.
- Conflicting booking dates should return 400.
- Invalid date range should return 400.

---

## Workflow 5.5: Check-In/Check-Out Time Recording (Fraud Prevention & Fee Calculation)
Controller: Short-termApartmentAPI/Controllers/BookingController.cs

### Purpose
Landlords/staff record actual guest arrival and departure times. System validates times are within ±1 day of scheduled dates, detects early/late occurrences, calculates fees, notifies parties, and prevents fraudulent time recording with 24-hour edit window and audit trail.

### Prerequisites
- Booking must be in "confirmed" or "paid" status
- Check-in time cannot be recorded before check-in date - 1 day
- Check-out time cannot be recorded until after check-in is recorded

### Landlord Steps
1. Record guest check-in time.
- Endpoint: POST /api/booking/{id}/check-in
- Auth: landlord or staff
- Input: ActualCheckIn (datetime), optional Notes
- Expected: 200 with BookingCheckTimeResponseDto
- Save: CheckInFee (if early), IsEditableWindow flag
- Verification: ActualCheckIn must be ±1 day from scheduled check-in date
- Verify: Tenant receives notification of recorded check-in time

2. Retrieve check-in/check-out details anytime.
- Endpoint: GET /api/booking/{id}/check-time
- Auth: authenticated (both parties can view)
- Expected: 200 with full check-time record including EditableWindow flag
- Verification: IsEditable = true only within 24 hours of RecordedAt timestamp

3. Update check-in time within 24-hour correction window.
- Endpoint: POST /api/booking/{id}/check-in (again, with new time)
- Auth: landlord or staff
- Expected: 200 with updated record if within 24 hours
- Verification: RecordedAt timestamp updates,  modification tracked internally

4. Record guest check-out time.
- Endpoint: POST /api/booking/{id}/check-out
- Auth: landlord or staff
- Input: ActualCheckOut (datetime), optional Notes
- Expected: 200 with BookingCheckTimeResponseDto
- Save: CheckOutFee (if late), LateCheckOutFee amount, booking status → "completed"
- Verification: ActualCheckOut must be ±1 day from scheduled check-out date
- Verification: ActualCheckIn must already be recorded
- Verify: Both tenant and landlord receive notifications with fee breakdown

5. Tenant reviews recorded times and disputes if needed.
- Endpoint: GET /api/booking/{id}/check-time
- Auth: tenant
- Expected: 200 with recorded times and fee info
- If tenant disagrees: Create support ticket via Workflow 9 with evidence

### Negative Tests
- Record check-in more than ±1 day from scheduled date should return 400.
- Record check-out without prior check-in should return 400 (InvalidOperationException).
- Attempt to edit record after 24-hour window should return 400.
- Staff attempts check-in on non-confirmed booking should return 400.
- Non-landlord/non-staff attempts to record times should return 403.
- Booking ID that doesn't exist should return 404.
- Invalid DateTime in actualCheckIn/actualCheckOut (e.g., year 2000) should return 400 (DTO validation).

### Fee Calculation Rules (Configuration in appsettings.json)
- **Early Check-In:** If ActualCheckIn < ScheduledCheckIn
	- Fee = (TotalPrice / Nights) × EarlyCheckInFeePercentOfDaily (default: 0.5 = 50%)
	- Example: $300 booking / 3 nights × 0.5 = $50 early check-in fee
- **Late Check-Out:** If ActualCheckOut > ScheduledCheckOut
	- Fee = (TotalPrice / Nights) × LateCheckOutFeePercentPerHour × hoursLate (default: 0.025 = 2.5% per hour)
	- Example: $300 booking / 3 nights × 0.025 × 2 hours late = $5 per hour × 2 = $10 fee

### Fraud Prevention Features
- **Time Validation:** Rejects times outside ±1 day of scheduled dates
- **24-Hour Correction Window:** Landlord can edit recorded times only within 24 hours; after that, only support staff can override
- **Audit Trail:** System tracks RecordedBy (staff user ID) and RecordedAt (server timestamp)
- **Notifications:** Both tenant and landlord notified immediately upon recording; tenant can verify and dispute
- **Dispute Resolution:** Tenant can open support ticket with evidence (photos, timestamps) to dispute fees

### Example Workflow
1. Booking scheduled check-in: 2026-03-29 14:00, check-out: 2026-04-01 12:00
2. Landlord records actual check-in: 2026-03-29 13:30 (30 min early)
	 - IsEarlyCheckIn = true
	 - EarlyCheckInFee = calculated
	 - Notification sent to tenant: "Guest arrived early at 13:30. Early check-in fee: $50"
3. Tenant views check-time details, sees IsEditable = true (within 24 hours)
4. Landlord records actual check-out: 2026-04-01 14:00 (2 hours late)
	 - IsLateCheckOut = true
	 - LateCheckOutFee = calculated ($10)
	 - Booking status → "completed"
	 - Notifications sent: Landlord receives fee alert, tenant receives courtesy notice
5. Tenant disputes late checkout; opens support ticket with photo evidence
6. Support staff reviews RecordedBy, RecordedAt, and audit trail; approves adjustment

### Audit Checks
- Verify RecordedBy contains valid staff/landlord user ID
- Verify RecordedAt is close to current timestamp (not backdated)
- Verify actual times do not exceed ±1 day boundaries
- Verify fees calculated correctly per configuration
- Verify booking status transitions from confirmed/paid → completed

---

## Workflow 6: Payment Processing Branches
Controllers:
- Short-termApartmentAPI/Controllers/StripeController.cs
- Short-termApartmentAPI/Controllers/MoMoController.cs

### Stripe Branch
1. Create checkout session.
- Endpoint: POST /api/stripe/checkout
- Auth: tenant
- Input: booking related entity id and amount
- Expected: 200 with session info

2. Simulate/trigger webhook callback.
- Endpoint: POST /api/stripe/webhook
- Auth: anonymous
- Expected: 200 and payment status updated to success

3. Verify booking/payment side effects via tenant/booking history endpoints.

### MoMo Branch
1. Create wallet payment.
- Endpoint: POST /create-wallet-payment
- Auth: none at controller level
- Expected: 200

2. Send invalid signature IPN.
- Endpoint: POST /ipn
- Expected: 400 invalid signature

3. Send valid success IPN.
- Endpoint: POST /ipn
- Expected: 200 and payment state transition success

### Negative Tests
- Stripe checkout with missing related entity id should return 400.
- Payment amount <= 0 should return 400.

---

## Workflow 7: Reviews
Controller: Short-termApartmentAPI/Controllers/ReviewController.cs

### Steps
1. Read public review list and apartment average rating.
- Endpoint: GET /api/review
- Endpoint: GET /api/review/apartment/{apartmentId}/average-rating
- Expected: 200

2. Tenant creates review.
- Endpoint: POST /api/review
- Auth: tenant
- Expected: 201

3. Reviewer updates own review.
- Endpoint: PUT /api/review/{id}
- Auth: tenant owner
- Expected: 204

4. Reviewer or admin deletes review.
- Endpoint: DELETE /api/review/{id}
- Expected: 204

### Negative Tests
- Non-owner tenant update/delete should return 403.
- Ineligible booking review should return 400/404.

---

## Workflow 8: Notifications
Controller: Short-termApartmentAPI/Controllers/NotificationController.cs

### Steps
1. Get current user notifications.
- Endpoint: GET /api/notification/my
- Auth: authenticated
- Expected: 200

2. Mark single notification as read.
- Endpoint: POST /api/notification/{id}/read
- Expected: 204

3. Mark all as read.
- Endpoint: POST /api/notification/read-all
- Expected: 204

### Negative Tests
- Missing token should return 401.
- Reading another user notification id should fail.

---

## Workflow 9: Support Ticket Lifecycle
Controller: Short-termApartmentAPI/Controllers/SupportTicketController.cs

### User Steps
1. Create support ticket.
- Endpoint: POST /api/supportticket
- Auth: authenticated
- Expected: 201

2. Get my tickets.
- Endpoint: GET /api/supportticket/my
- Auth: authenticated
- Expected: 200

3. Report persisting issue.
- Endpoint: POST /api/supportticket/{id}/report-persisting
- Auth: authenticated
- Expected: 201 for follow-up ticket

### Staff/Admin Steps
4. View all tickets.
- Endpoint: GET /api/supportticket
- Auth: staff or admin
- Expected: 200

5. Update ticket.
- Endpoint: PUT /api/supportticket/{id}
- Auth: staff or admin
- Expected: 204

### Negative Tests
- Standard user tries GET /api/supportticket should return 403.
- User accesses another user ticket id should be denied.

---

## Workflow 10: Property Inspection Lifecycle
Controller: Short-termApartmentAPI/Controllers/PropertyInspectionController.cs

### Steps
1. Admin creates inspection.
- Endpoint: POST /api/propertyinspection
- Auth: admin
- Expected: 201 and status scheduled

2. Staff starts inspection.
- Endpoint: POST /api/propertyinspection/{id}/start
- Auth: staff
- Expected: 200 and in_progress

3. Staff completes inspection.
- Endpoint: POST /api/propertyinspection/{id}/complete
- Auth: staff
- Expected: 200 with completion state

4. Admin reviews inspection.
- Endpoint: POST /api/propertyinspection/{id}/review
- Auth: admin
- Expected: 200

### Negative Tests
- Unauthorized role for staff/admin actions should return 403.
- Invalid state transition should return 400.

---

## Workflow 11: Landlord Histories and Subscription
Controller: Short-termApartmentAPI/Controllers/LandlordController.cs

### Steps
1. Get landlord profile.
- Endpoint: GET /api/landlord/me
- Auth: landlord
- Expected: 200

2. Get own apartments.
- Endpoint: GET /api/landlord/apartments
- Expected: 200

3. Get current subscription.
- Endpoint: GET /api/landlord/subscription
- Expected: 200 or 404 if none

4. Start subscription checkout.
- Endpoint: POST /api/landlord/subscription/momo-checkout
- Expected: 200

5. Get subscription history.
- Endpoint: GET /api/landlord/subscriptions/history
- Expected: 200

6. Get payment history and booking history.
- Endpoint: GET /api/landlord/payments/history
- Endpoint: GET /api/landlord/bookings/history
- Expected: 200

### Negative Tests
- Non-landlord token on landlord endpoints should return 403.

---

## Workflow 12: Admin Backoffice Management
Controllers:
- Short-termApartmentAPI/Controllers/AdminController.cs
- Short-termApartmentAPI/Controllers/UserController.cs
- Short-termApartmentAPI/Controllers/SubscriptionPlanController.cs
- Short-termApartmentAPI/Controllers/ReportsController.cs
- Short-termApartmentAPI/Controllers/PackageController.cs
- Short-termApartmentAPI/Controllers/PackageItemController.cs

### Steps
1. Admin ping.
- Endpoint: GET /api/admin/ping
- Expected: 200

2. User management CRUD.
- Endpoints: GET/POST/PUT/DELETE /api/user...
- Expected: success for admin only

3. Subscription plan management CRUD.
- Endpoints: GET/POST/PUT/DELETE /api/subscriptionplan...
- Expected: success for admin only

4. Package and package item management.
- Endpoints: /api/package... and /api/packageitem...
- Expected: role-enforced success

5. Reports catalog/create/run.
- Endpoints: GET /api/reports/catalog, POST /api/reports, POST /api/reports/{id}/run
- Expected: 200/201 for admin

### Negative Tests
- Non-admin token should fail on admin-only routes.

---

## Workflow 13: Public Discovery and Read-Only Coverage
Controllers:
- Short-termApartmentAPI/Controllers/ApartmentController.cs
- Short-termApartmentAPI/Controllers/AmenityController.cs
- Short-termApartmentAPI/Controllers/RoomController.cs
- Short-termApartmentAPI/Controllers/NearbyAttractionController.cs
- Short-termApartmentAPI/Controllers/PackageController.cs
- Short-termApartmentAPI/Controllers/PackageItemController.cs
- Short-termApartmentAPI/Controllers/ReviewController.cs

### Steps
1. Run all anonymous GET list/detail endpoints.
2. Confirm responses contain expected pagination and object shape.
3. Confirm restricted write endpoints reject anonymous requests.

---

## Smoke Suite (High Priority)
Run this minimal chain first on each deployment:
1. Auth login for landlord, tenant, admin, staff.
2. Tenant identity doc upload and staff approval.
3. Landlord create apartment and submit for review.
4. Admin approve listing.
5. Tenant quote and create booking.
6. Trigger one payment callback path (Stripe webhook or MoMo IPN success).
7. Verify booking/payment status reflects success.

---

## Full Regression Suite
After smoke passes, run all workflows 1 through 13 including all negative tests.

---

## Test Case Template
Use this template for each endpoint test execution record:

- TestCaseId:
- Workflow:
- Endpoint:
- Method:
- Role Token Used:
- Preconditions:
- Request Payload:
- Expected Status Code:
- Expected Response Body:
- Expected State Change:
- Actual Result:
- Evidence (screenshots/log id):
- Pass or Fail:

---

## Known Critical Business Rules To Verify
- Tenant must be identity-verified before booking creation.
- Only posted apartments can be booked.
- Listing submission requires minimum completeness checks.
- Ownership checks must prevent cross-account mutations.
- Payment callbacks must update payment state and downstream booking/subscription state.
