using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Common.Utils;

namespace Common.DTOs;

/// <summary>
/// DTO for creating a guest check-time request (early check-in or late check-out).
/// </summary>
public class CreateCheckTimeRequestDto : IValidatableObject
{
    /// <summary>
    /// Type of request: "EarlyCheckIn" or "LateCheckOut"
    /// </summary>
    [Required]
    [StringLength(50)]
    public string RequestType { get; set; } = null!;

    /// <summary>
    /// The requested check-in or check-out time
    /// </summary>
    [Required]
    public DateTime RequestedTime { get; set; }

    /// <summary>
    /// Guest's reason for the request (optional, for landlord context)
    /// </summary>
    [StringLength(500)]
    public string? GuestReason { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Validate that RequestedTime is in the future
        if (RequestedTime <= VietnamTime.Now)
        {
            yield return new ValidationResult("Requested time must be in the future.", new[] { nameof(RequestedTime) });
        }

        // Validate RequestType
        if (!new[] { "EarlyCheckIn", "LateCheckOut" }.Contains(RequestType))
        {
            yield return new ValidationResult("RequestType must be 'EarlyCheckIn' or 'LateCheckOut'.", new[] { nameof(RequestType) });
        }
    }
}

/// <summary>
/// DTO for landlord counter-offering a different time and/or fee.
/// </summary>
public class CounterCheckTimeDto : IValidatableObject
{
    /// <summary>
    /// Counter-offered check-in or check-out time
    /// </summary>
    [Required]
    public DateTime CounterOfferedTime { get; set; }

    /// <summary>
    /// Counter-offered fee (can be 0 if waived)
    /// </summary>
    [Range(0, 10000000)]
    public decimal? CounterOfferedFee { get; set; }

    /// <summary>
    /// Landlord's response message (optional)
    /// </summary>
    [StringLength(500)]
    public string? HostResponse { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CounterOfferedTime <= VietnamTime.Now)
        {
            yield return new ValidationResult("Counter-offered time must be in the future.", new[] { nameof(CounterOfferedTime) });
        }

        if (CounterOfferedFee.HasValue && CounterOfferedFee < 0)
        {
            yield return new ValidationResult("Fee cannot be negative.", new[] { nameof(CounterOfferedFee) });
        }
    }
}

/// <summary>
/// DTO for approving a check-time request (with optional final fee adjustment).
/// </summary>
public class ApproveCheckTimeDto
{
    /// <summary>
    /// Final agreed fee (can override counter-offer fee or RequestedTime fee)
    /// </summary>
    [Range(0, 10000000)]
    public decimal? AgreedFee { get; set; }

    /// <summary>
    /// Landlord's approval response (optional)
    /// </summary>
    [StringLength(500)]
    public string? HostResponse { get; set; }
}

/// <summary>
/// DTO for guest accepting a counter-offer.
/// </summary>
public class AcceptCounterDto
{
    /// <summary>
    /// Optional notes from guest
    /// </summary>
    [StringLength(500)]
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for rejecting a check-time request.
/// </summary>
public class RejectCheckTimeDto
{
    /// <summary>
    /// Reason for rejection
    /// </summary>
    [StringLength(500)]
    public string? Reason { get; set; }
}

/// <summary>
/// DTO for responding with check-time request details.
/// </summary>
public class CheckTimeRequestResponseDto
{
    public Guid Id { get; set; }

    public Guid BookingId { get; set; }

    public string RequestType { get; set; } = null!;

    public DateTime RequestedTime { get; set; }

    public DateTime? CounterOfferedTime { get; set; }

    public decimal? CounterOfferedFee { get; set; }

    public DateTime? AgreedTime { get; set; }

    public decimal? AgreedFee { get; set; }

    public string Status { get; set; } = null!;

    public string? GuestReason { get; set; }

    public string? HostResponse { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public Guid? ProcessedById { get; set; }

    // Display-friendly fields
    public bool IsExpired => Status == "CounterOffered" && ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;

    public string DisplayStatus => IsExpired ? "Expired" : Status;
}
