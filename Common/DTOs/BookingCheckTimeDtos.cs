using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Common.DTOs;

/// <summary>
/// DTO for recording actual check-in time for a booking.
/// </summary>
public class RecordCheckInDto : IValidatableObject
{
    [Required]
    public DateTime ActualCheckIn { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    /// <summary>
    /// URL of uploaded photo evidence (controller should upload file and set this).
    /// </summary>
    public string? PhotoEvidenceUrl { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Validate that ActualCheckIn is a reasonable timestamp
        if (ActualCheckIn.Year < 2020 || ActualCheckIn.Year > Common.Utils.VietnamTime.Now.Year + 1)
        {
            yield return new ValidationResult("Check-in date year is invalid.", new[] { nameof(ActualCheckIn) });
        }

        if (ActualCheckIn > Common.Utils.VietnamTime.Now.AddMinutes(5))
        {
            yield return new ValidationResult("Check-in time cannot be in the future (+5 min grace).", new[] { nameof(ActualCheckIn) });
        }

        if (string.IsNullOrWhiteSpace(PhotoEvidenceUrl))
        {
            yield return new ValidationResult("PhotoEvidenceUrl is required.", new[] { nameof(PhotoEvidenceUrl) });
        }
    }
}

/// <summary>
/// DTO for recording actual check-out time for a booking.
/// </summary>
public class RecordCheckOutDto : IValidatableObject
{
    [Required]
    public DateTime ActualCheckOut { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    /// <summary>
    /// URL of uploaded photo evidence (controller should upload file and set this).
    /// </summary>
    public string? PhotoEvidenceUrl { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Validate that ActualCheckOut is a reasonable timestamp
        if (ActualCheckOut.Year < 2020 || ActualCheckOut.Year > Common.Utils.VietnamTime.Now.Year + 1)
        {
            yield return new ValidationResult("Check-out date year is invalid.", new[] { nameof(ActualCheckOut) });
        }

        if (ActualCheckOut > Common.Utils.VietnamTime.Now.AddMinutes(5))
        {
            yield return new ValidationResult("Check-out time cannot be in the future (+5 min grace).", new[] { nameof(ActualCheckOut) });
        }

        if (string.IsNullOrWhiteSpace(PhotoEvidenceUrl))
        {
            yield return new ValidationResult("PhotoEvidenceUrl is required.", new[] { nameof(PhotoEvidenceUrl) });
        }
    }
}

/// <summary>
/// DTO for tenant self-declaration of arrival (not landlord-certified check-in).
/// </summary>
public class ConfirmGuestArrivalDto : IValidatableObject
{
    [Required]
    public DateTime ArrivedAt { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [MaxLength(2000)]
    public string? PhotoEvidenceUrl { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ArrivedAt.Year < 2020 || ArrivedAt.Year > Common.Utils.VietnamTime.Now.Year + 1)
        {
            yield return new ValidationResult("Arrival date year is invalid.", new[] { nameof(ArrivedAt) });
        }

        if (ArrivedAt > Common.Utils.VietnamTime.Now.AddMinutes(5))
        {
            yield return new ValidationResult("Arrival time cannot be in the future (+5 min grace).", new[] { nameof(ArrivedAt) });
        }
    }
}

/// <summary>
/// DTO for responding with booking check-in/check-out details and calculated fees.
/// </summary>
public class BookingCheckTimeResponseDto
{
    public Guid CheckTimeId { get; set; }
    public Guid BookingId { get; set; }

    public DateTime ScheduledCheckIn { get; set; }
    public DateTime ScheduledCheckOut { get; set; }

    public DateTime? ActualCheckIn { get; set; }
    public DateTime? ActualCheckOut { get; set; }

    public bool? IsLateCheckIn { get; set; }

    public bool? IsEarlyCheckIn { get; set; }
    public decimal? EarlyCheckInFee { get; set; }

    public bool? IsLateCheckOut { get; set; }
    public decimal? LateCheckOutFee { get; set; }

    /// <summary>
    /// Total additional fee from early check-in and late check-out.
    /// </summary>
    public decimal TotalFee { get; set; }

    /// <summary>
    /// Settlement state for the accumulated check-time fee: none, due, disputed, paid, waived.
    /// </summary>
    public string FeeSettlementStatus { get; set; } = "none";

    public DateTime? FeeDueAt { get; set; }
    public DateTime? FeeSettledAt { get; set; }
    public string? FeeSettlementNotes { get; set; }

    /// <summary>
    /// Indicates whether the fee is past due and should block future bookings for this tenant.
    /// </summary>
    public bool ManualSettlementRequired { get; set; }

    public Guid? RecordedBy { get; set; }
    public DateTime? RecordedAt { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// JSON array tracking all modifications: { timestamp, recordedBy, oldTime, newTime }
    /// </summary>
    public string? CheckInModifications { get; set; }

    /// <summary>
    /// JSON array tracking all modifications: { timestamp, recordedBy, oldTime, newTime }
    /// </summary>
    public string? CheckOutModifications { get; set; }

    /// <summary>
    /// URLs for the uploaded photo evidence for check-in and check-out.
    /// </summary>
    public string? CheckInPhotoUrl { get; set; }
    public string? CheckOutPhotoUrl { get; set; }

    public DateTime? GuestArrivalConfirmedAt { get; set; }
    public Guid? GuestArrivalConfirmedBy { get; set; }
    public string? GuestArrivalNotes { get; set; }
    public string? GuestArrivalPhotoUrl { get; set; }

    /// <summary>
    /// Claim metadata for the checkout snapshot.
    /// </summary>
    public DateTime? ClaimOpenedAt { get; set; }
    public DateTime? ClaimExpiresAt { get; set; }
    public DateTime? ClaimLockedAt { get; set; }
    public string ClaimStatus { get; set; } = "open";
    public bool ClaimIsLocked { get; set; }
    public bool ClaimIsExpired { get; set; }

    public string? NoShowStatus { get; set; }
    public Guid? NoShowMarkedBy { get; set; }
    public DateTime? NoShowMarkedAt { get; set; }
    public string? MissingCheckOutStatus { get; set; }
    public DateTime? AutoClosedAt { get; set; }

    public DateTime? LastModifiedAt { get; set; }

    /// <summary>
    /// Indicates if the recorded times are still editable (within 24 hours of RecordedAt).
    /// </summary>
    public bool IsEditable { get; set; }

    /// <summary>
    /// Tenant response status for the recorded check-time values: pending, confirmed, disputed.
    /// </summary>
    public string TenantResponseStatus { get; set; } = "pending";

    public Guid? TenantRespondedBy { get; set; }
    public DateTime? TenantRespondedAt { get; set; }

    public string? TenantDisputeReason { get; set; }
    public string? TenantDisputeNotes { get; set; }

    /// <summary>
    /// Dispute resolution state controlled by staff/admin: open, resolved_in_favor_of_tenant, resolved_in_favor_of_landlord.
    /// </summary>
    public string? DisputeResolutionStatus { get; set; }

    public Guid? DisputeResolvedBy { get; set; }
    public DateTime? DisputeResolvedAt { get; set; }
    public string? DisputeResolutionNotes { get; set; }

    /// <summary>
    /// If payment is required and a checkout session was created, provide a URL for tenant to complete payment.
    /// </summary>
    public string? PaymentRedirectUrl { get; set; }

    /// <summary>
    /// Pending payment record id if payment was initiated but not yet settled.
    /// </summary>
    public Guid? PendingPaymentId { get; set; }
}

/// <summary>
/// Tenant response payload for recorded check-time values.
/// </summary>
public class RespondBookingCheckTimeDto : IValidatableObject
{
    [Required]
    [RegularExpression("^(confirm|refute|dispute)$", ErrorMessage = "Action must be 'confirm', 'refute', or 'dispute'.")]
    public string Action { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? DisputeReason { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if ((string.Equals(Action, "refute", StringComparison.OrdinalIgnoreCase) || string.Equals(Action, "dispute", StringComparison.OrdinalIgnoreCase)) && string.IsNullOrWhiteSpace(DisputeReason))
        {
            yield return new ValidationResult("DisputeReason is required when action is 'refute' or 'dispute'.", new[] { nameof(DisputeReason) });
        }
    }
}

/// <summary>
/// Staff/admin resolution payload for tenant check-time disputes.
/// </summary>
public class ResolveBookingCheckTimeDisputeDto
{
    [Required]
    public bool ApproveTenantDispute { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>
/// Manual settlement payload for a booking check-time fee.
/// </summary>
public class SettleBookingCheckTimeFeeDto : IValidatableObject
{
    [Required]
    [RegularExpression("^(paid|waived)$", ErrorMessage = "SettlementAction must be 'paid' or 'waived'.")]
    public string SettlementAction { get; set; } = "paid";

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(SettlementAction))
        {
            yield return new ValidationResult("SettlementAction is required.", new[] { nameof(SettlementAction) });
        }
    }
}

/// <summary>
/// Landlord payment confirmation payload for reporting that a check-time fee has been paid.
/// </summary>
public class LandlordPaymentConfirmationDto : IValidatableObject
{

    [Required]
    public DateTime PaymentDate { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PaymentDate > Common.Utils.VietnamTime.Now)
        {
            yield return new ValidationResult("Payment date cannot be in the future.", new[] { nameof(PaymentDate) });
        }
    }
}

/// <summary>
/// Tenant payment payload for settling a locked claim fee.
/// </summary>
public class PayClaimFeeDto : IValidatableObject
{
    [MaxLength(100)]
    public string? PaymentReference { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    /// <summary>
    /// Preferred payment method: 'stripe' or 'momo'. Optional; defaults to configured gateway.
    /// </summary>
    [MaxLength(50)]
    public string? PaymentMethod { get; set; }

    /// <summary>
    /// Device platform for redirect payload: 'web', 'ios', 'android'. Required for Stripe deep links.
    /// </summary>
    [MaxLength(20)]
    public string? DevicePlatform { get; set; }

    /// <summary>
    /// Optional return URL to override configured success/cancel URLs.
    /// </summary>
    [MaxLength(2000)]
    public string? ReturnUrl { get; set; }

    /// <summary>
    /// Optional cancel URL to override configured cancel URLs.
    /// </summary>
    [MaxLength(2000)]
    public string? CancelUrl { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        yield break;
    }
}

/// <summary>
/// Payload for marking a booking as no-show when check-in never happened.
/// </summary>
public class MarkNoShowDto : IValidatableObject
{
    [MaxLength(300)]
    public string? Reason { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Reason) && string.IsNullOrWhiteSpace(Notes))
        {
            yield return new ValidationResult("Either Reason or Notes is required.", new[] { nameof(Reason), nameof(Notes) });
        }
    }
}

/// <summary>
/// Payload for manually closing missing check-out records.
/// </summary>
public class CloseMissingCheckOutDto : IValidatableObject
{
    public bool Force { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Force && string.IsNullOrWhiteSpace(Notes))
        {
            yield return new ValidationResult("Notes are required unless Force is true.", new[] { nameof(Notes) });
        }
    }
}

/// <summary>
/// DTO representing a single booking's outstanding check-time fees.
/// </summary>
public class OutstandingCheckTimeFeeItemDto
{
    public Guid BookingId { get; set; }
    public Guid ApartmentId { get; set; }
    public string? ApartmentAddress { get; set; }
    
    public DateTime ScheduledCheckIn { get; set; }
    public DateTime ScheduledCheckOut { get; set; }
    
    public decimal EarlyCheckInFee { get; set; }
    public decimal LateCheckOutFee { get; set; }
    public decimal TotalFee { get; set; }
    
    /// <summary>
    /// Settlement status: none, due, disputed, paid, waived, payment_submitted_pending_verification
    /// </summary>
    public string FeeSettlementStatus { get; set; } = "none";
    
    public DateTime? FeeDueAt { get; set; }
    public DateTime? FeeSettledAt { get; set; }
    public bool IsOverdue { get; set; }
    
    public string? TenantDisputeReason { get; set; }
    public string? DisputeResolutionStatus { get; set; }
}

/// <summary>
/// DTO for listing all outstanding check-time fees for a user across multiple bookings.
/// </summary>
public class OutstandingCheckTimeFeesResponseDto
{
    public Guid UserId { get; set; }
    public decimal TotalOutstandingFees { get; set; }
    public int TotalOutstandingCount { get; set; }
    public int OverdueCount { get; set; }
    public int DisputedCount { get; set; }
    
    public List<OutstandingCheckTimeFeeItemDto> OutstandingFees { get; set; } = new();
}

/// <summary>
/// DTO representing a tenant's outstanding check-time fees for a landlord's property.
/// </summary>
public class OutstandingCheckTimeFeeByTenantDto
{
    public Guid BookingId { get; set; }
    public Guid ApartmentId { get; set; }
    public string? ApartmentAddress { get; set; }
    
    public Guid TenantId { get; set; }
    public string? TenantName { get; set; }
    
    public DateTime ScheduledCheckIn { get; set; }
    public DateTime ScheduledCheckOut { get; set; }
    
    public decimal EarlyCheckInFee { get; set; }
    public decimal LateCheckOutFee { get; set; }
    public decimal TotalFee { get; set; }
    
    /// <summary>
    /// Settlement status: none, due, disputed, paid, waived, payment_submitted_pending_verification
    /// </summary>
    public string FeeSettlementStatus { get; set; } = "none";
    
    public DateTime? FeeDueAt { get; set; }
    public DateTime? FeeSettledAt { get; set; }
    public bool IsOverdue { get; set; }
    
    public string? TenantDisputeReason { get; set; }
    public string? DisputeResolutionStatus { get; set; }
}

/// <summary>
/// DTO for listing all outstanding check-time fees for a landlord across all their properties.
/// Aggregates fees by tenant.
/// </summary>
public class LandlordOutstandingCheckTimeFeesResponseDto
{
    public Guid LandlordId { get; set; }
    public decimal TotalOutstandingFees { get; set; }
    public int TotalOutstandingCount { get; set; }
    public int OverdueCount { get; set; }
    public int DisputedCount { get; set; }
    public int UniqueTenantCount { get; set; }
    public decimal WalletPenaltyTotalAmount { get; set; }
    public int WalletPenaltyCount { get; set; }
    
    public List<OutstandingCheckTimeFeeByTenantDto> OutstandingFees { get; set; } = new();
    public List<LandlordWalletPenaltyTransactionDto> WalletPenaltyTransactions { get; set; } = new();
}

public class LandlordWalletPenaltyTransactionDto
{
    public Guid PaymentId { get; set; }
    public Guid BookingId { get; set; }
    public Guid ApartmentId { get; set; }
    public string? ApartmentAddress { get; set; }
    public Guid TenantId { get; set; }
    public string? TenantName { get; set; }
    public decimal Amount { get; set; }
    public string PaymentType { get; set; } = string.Empty;
    public string PaymentPurpose { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string? Status { get; set; }
    public string? TransactionId { get; set; }
    public DateTime? PaidAt { get; set; }
}
