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
        if (ActualCheckIn.Year < 2020 || ActualCheckIn.Year > DateTime.UtcNow.Year + 1)
        {
            yield return new ValidationResult("Check-in date year is invalid.", new[] { nameof(ActualCheckIn) });
        }

        if (ActualCheckIn > DateTime.UtcNow.AddMinutes(5))
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
        if (ActualCheckOut.Year < 2020 || ActualCheckOut.Year > DateTime.UtcNow.Year + 1)
        {
            yield return new ValidationResult("Check-out date year is invalid.", new[] { nameof(ActualCheckOut) });
        }

        if (ActualCheckOut > DateTime.UtcNow.AddMinutes(5))
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
}
