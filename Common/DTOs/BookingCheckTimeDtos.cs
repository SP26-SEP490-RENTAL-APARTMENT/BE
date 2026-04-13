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

    public bool? IsEarlyCheckIn { get; set; }
    public decimal? EarlyCheckInFee { get; set; }

    public bool? IsLateCheckOut { get; set; }
    public decimal? LateCheckOutFee { get; set; }

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
}

/// <summary>
/// Tenant response payload for recorded check-time values.
/// </summary>
public class RespondBookingCheckTimeDto : IValidatableObject
{
    [Required]
    [RegularExpression("^(confirm|dispute)$", ErrorMessage = "Action must be 'confirm' or 'dispute'.")]
    public string Action { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? DisputeReason { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.Equals(Action, "dispute", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(DisputeReason))
        {
            yield return new ValidationResult("DisputeReason is required when action is 'dispute'.", new[] { nameof(DisputeReason) });
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
