using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Common.Enums;
using Microsoft.AspNetCore.Http;

namespace Common.DTOs;

public class CreateBookingRequestDto : IValidatableObject
{

    [Required]
    public Guid ApartmentId { get; set; }

    public DateOnly? CheckInDate { get; set; }

    public DateOnly? CheckOutDate { get; set; }

    public DateTime? CheckInDateTime { get; set; }

    public DateTime? CheckOutDateTime { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Nights must be at least 1.")]
    public int? Nights { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "At least 1 adult is required.")]
    public int? NoOfAdults { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Number of children cannot be negative.")]
    public int? NoOfChildren { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Number of infants cannot be negative.")]
    public int? NoOfInfants { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Number of pets cannot be negative.")]
    public int? NoOfPets { get; set; }

    public Guid? PackageId { get; set; }

    public BookingPaymentMode PaymentMode { get; set; } = BookingPaymentMode.partial;

    [MaxLength(20)]
    public string? PaymentProvider { get; set; }

    [MaxLength(20)]
    public string? DevicePlatform { get; set; }

    [MaxLength(2048)]
    public string? ReturnUrl { get; set; }

    [MaxLength(2048)]
    public string? CancelUrl { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var hasDateTimes = CheckInDateTime.HasValue || CheckOutDateTime.HasValue;
        var hasDates = CheckInDate.HasValue || CheckOutDate.HasValue;

        if (hasDateTimes)
        {
            if (!CheckInDateTime.HasValue || !CheckOutDateTime.HasValue)
            {
                yield return new ValidationResult("Both check-in and check-out date-time are required when using date-time booking.", new[] { nameof(CheckInDateTime), nameof(CheckOutDateTime) });
                yield break;
            }

            if (CheckInDateTime.Value < Common.Utils.VietnamTime.Now)
            {
                yield return new ValidationResult("Check-in date-time cannot be earlier than now.", new[] { nameof(CheckInDateTime) });
            }

            if (CheckInDateTime.Value >= CheckOutDateTime.Value)
            {
                yield return new ValidationResult("Check-out date-time must be later than check-in date-time.", new[] { nameof(CheckOutDateTime) });
            }

            if ((CheckOutDateTime.Value - CheckInDateTime.Value).TotalDays > 30)
            {
                yield return new ValidationResult("Booking duration cannot exceed 30 days.", new[] { nameof(CheckOutDateTime) });
            }

            yield break;
        }

        if (!hasDates)
        {
            yield return new ValidationResult("Provide either check-in/check-out dates or check-in/check-out date-times.", new[] { nameof(CheckInDate), nameof(CheckOutDate), nameof(CheckInDateTime), nameof(CheckOutDateTime) });
            yield break;
        }

        if (!CheckInDate.HasValue || !CheckOutDate.HasValue)
        {
            yield return new ValidationResult("Both check-in and check-out dates are required.", new[] { nameof(CheckInDate), nameof(CheckOutDate) });
            yield break;
        }

        var today = DateOnly.FromDateTime(Common.Utils.VietnamTime.TodayDateTime);
        if (CheckInDate.Value < today)
        {
            yield return new ValidationResult("Check-in date cannot be earlier than today.", new[] { nameof(CheckInDate) });
        }

        if (CheckInDate.Value >= CheckOutDate.Value)
        {
            yield return new ValidationResult("Check-out date must be later than check-in date.", new[] { nameof(CheckOutDate) });
        }

        var nights = CheckOutDate.Value.DayNumber - CheckInDate.Value.DayNumber;
        if (nights > 30)
        {
            yield return new ValidationResult("Booking duration cannot exceed 30 days.", new[] { nameof(CheckOutDate) });
        }
    }
}

public class UpdateBookingRequestDto : IValidatableObject
{
    public DateOnly? CheckInDate { get; set; }
    public DateOnly? CheckOutDate { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Nights must be at least 1.")]
    public int? Nights { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "At least 1 adult is required.")]
    public int? NoOfAdults { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Number of children cannot be negative.")]
    public int? NoOfChildren { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Number of infants cannot be negative.")]
    public int? NoOfInfants { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Number of pets cannot be negative.")]
    public int? NoOfPets { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Total price must be a non-negative value.")]
    public decimal? TotalPrice { get; set; }

    public Guid? PackageId { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Package price must be a non-negative value.")]
    public decimal? PackagePrice { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Deposit amount must be a non-negative value.")]
    public decimal? DepositAmount { get; set; }

    public bool? DepositPaid { get; set; }

    public DateOnly? BalanceDueDate { get; set; }

    [MaxLength(50)]
    public string? Status { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var today = DateOnly.FromDateTime(Common.Utils.VietnamTime.TodayDateTime);
        if (CheckInDate.HasValue && CheckInDate.Value < today)
        {
            yield return new ValidationResult("Check-in date cannot be earlier than today.", new[] { nameof(CheckInDate) });
        }

        if (CheckInDate.HasValue && CheckOutDate.HasValue && CheckInDate.Value >= CheckOutDate.Value)
        {
            yield return new ValidationResult("Check-out date must be later than check-in date.", new[] { nameof(CheckOutDate) });
        }
    }
}

public class BookingResponseDto
{
    public Guid BookingId { get; set; }
    public Guid TenantId { get; set; }
    public string? TenantFullName { get; set; }
    public DateTime? ActualCheckIn { get; set; }
    public DateTime? ActualCheckOut { get; set; }
    public Guid ApartmentId { get; set; }
    public Guid? TicketId { get; set; }
    public List<string> Images { get; set; } = new();
    public DateOnly CheckInDate { get; set; }
    public DateOnly CheckOutDate { get; set; }
    public int Nights { get; set; }
    public int? NoOfAdults { get; set; }
    public int? NoOfChildren { get; set; }
    public int? NoOfInfants { get; set; }
    public int? NoOfPets { get; set; }
    public decimal TotalPrice { get; set; }
    public Guid? PackageId { get; set; }
    public decimal? PackagePrice { get; set; }
    public decimal DepositAmount { get; set; }
    public decimal UpfrontPaymentAmount { get; set; }
    public bool? DepositPaid { get; set; }
    public string? PaymentMode { get; set; }
    public DateOnly BalanceDueDate { get; set; }
    public bool IsRefundable { get; set; }
    public string? Status { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class BookingPaymentLinkDto
{
    public string Provider { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Deeplink { get; set; }
    public string? QrCodeUrl { get; set; }
    public string? TransactionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? PaymentId { get; set; }
}

public class ReconcileBookingPaymentRequestDto
{
    [Required]
    public string? OrderId { get; set; }

    [Required]
    public string? RequestId { get; set; }

    [Required]
    public string? ExtraData { get; set; }

    public int ResultCode { get; set; }

    [MaxLength(1024)]
    public string? Message { get; set; }

    [MaxLength(100)]
    public string? TransId { get; set; }
}

public class CreateBookingResponseDto
{
    public BookingResponseDto Booking { get; set; } = null!;
    public BookingPaymentLinkDto? PaymentLink { get; set; }
}

public class RequestBookingRefundDto : IValidatableObject
{
    [Required]
    [MaxLength(50)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [MaxLength(100)]
    public string? PayOsReceiverName { get; set; }

    [MaxLength(50)]
    public string? PayOsBankCode { get; set; }

    [MaxLength(50)]
    public string? PayOsAccountNumber { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var normalizedReason = Reason.Trim().ToLowerInvariant();
        var allowedReasons = new[] { "tenant_request", "system_cancellation", "manual_admin" };

        if (string.IsNullOrWhiteSpace(Reason) || !allowedReasons.Contains(normalizedReason))
        {
            yield return new ValidationResult(
                "Reason must be one of: tenant_request, system_cancellation, manual_admin.",
                new[] { nameof(Reason) });
        }
    }
}

public class BookingRefundResponseDto
{
    public Guid BookingId { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalPaidAmount { get; set; }
    public decimal ProcessingFeeAmount { get; set; }
    public decimal NetRefundAmount { get; set; }
    public int RefundedPaymentCount { get; set; }
    public DateTime ProcessedAt { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class BookingQuoteRequestDto : IValidatableObject
{
    [Required]
    public Guid ApartmentId { get; set; }

    public Guid? PackageId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "At least 1 adult is required.")]
    public int? NoOfAdults { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Number of children cannot be negative.")]
    public int? NoOfChildren { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Number of infants cannot be negative.")]
    public int? NoOfInfants { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Number of pets cannot be negative.")]
    public int? NoOfPets { get; set; }

    public DateOnly? CheckInDate { get; set; }

    public DateOnly? CheckOutDate { get; set; }

    public DateTime? CheckInDateTime { get; set; }

    public DateTime? CheckOutDateTime { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var hasDateTimes = CheckInDateTime.HasValue || CheckOutDateTime.HasValue;
        var hasDates = CheckInDate.HasValue || CheckOutDate.HasValue;

        if (hasDateTimes)
        {
            if (!CheckInDateTime.HasValue || !CheckOutDateTime.HasValue)
            {
                yield return new ValidationResult("Both check-in and check-out date-time are required when using date-time quote.", new[] { nameof(CheckInDateTime), nameof(CheckOutDateTime) });
                yield break;
            }

            if (CheckInDateTime.Value < Common.Utils.VietnamTime.Now)
            {
                yield return new ValidationResult("Check-in date-time cannot be earlier than now.", new[] { nameof(CheckInDateTime) });
            }

            if (CheckInDateTime.Value >= CheckOutDateTime.Value)
            {
                yield return new ValidationResult("Check-out date-time must be later than check-in date-time.", new[] { nameof(CheckOutDateTime) });
            }

            if ((CheckOutDateTime.Value - CheckInDateTime.Value).TotalDays > 30)
            {
                yield return new ValidationResult("Booking duration cannot exceed 30 days.", new[] { nameof(CheckOutDateTime) });
            }

            yield break;
        }

        if (!hasDates)
        {
            yield return new ValidationResult("Provide either check-in/check-out dates or check-in/check-out date-times.", new[] { nameof(CheckInDate), nameof(CheckOutDate), nameof(CheckInDateTime), nameof(CheckOutDateTime) });
            yield break;
        }

        if (!CheckInDate.HasValue || !CheckOutDate.HasValue)
        {
            yield return new ValidationResult("Both check-in and check-out dates are required.", new[] { nameof(CheckInDate), nameof(CheckOutDate) });
            yield break;
        }

        var today = DateOnly.FromDateTime(Common.Utils.VietnamTime.TodayDateTime);
        if (CheckInDate.Value < today)
        {
            yield return new ValidationResult("Check-in date cannot be earlier than today.", new[] { nameof(CheckInDate) });
        }

        if (CheckInDate.Value >= CheckOutDate.Value)
        {
            yield return new ValidationResult("Check-out date must be later than check-in date.", new[] { nameof(CheckOutDate) });
        }

        var nights = CheckOutDate.Value.DayNumber - CheckInDate.Value.DayNumber;
        if (nights > 30)
        {
            yield return new ValidationResult("Booking duration cannot exceed 30 days.", new[] { nameof(CheckOutDate) });
        }
    }
}

public class BookingQuoteResponseDto
{
    public Guid ApartmentId { get; set; }
    public Guid? PackageId { get; set; }
    public int Nights { get; set; }
    public decimal BasePricePerNight { get; set; }
    public decimal ResolvedPricePerNight { get; set; }
    public decimal BaseAmount { get; set; }
    public decimal PackageAmount { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal SuggestedDeposit { get; set; }
    public decimal RemainingBalance { get; set; }
    public decimal FullUpfrontPaymentAmount { get; set; }
    public decimal FullUpfrontLandlordShareAmount { get; set; }
    public List<DailyPriceResolutionDto> PriceCalendar { get; set; } = new();
}

public class SubmitResidenceReportDto
{
    [Required]
    public bool ReportedToPolice { get; set; }

    public DateOnly? ReportDate { get; set; }

    [MaxLength(100)]
    public string? ReportNumber { get; set; }

    public DateTime? ActualCheckIn { get; set; }
}

public class TemporaryResidenceReportDetailsDto
{
    public Guid ReportId { get; set; }

    public Guid BookingId { get; set; }

    public Guid LandlordId { get; set; }

    public Guid TenantId { get; set; }

    public string? TenantFullName { get; set; }

    public string TenantPassportId { get; set; } = null!;

    public DateOnly? TenantDateOfBirth { get; set; }

    public string? TenantNationalIdCardNumber { get; set; }

    public string TenantNationality { get; set; } = null!;

    public string? TenantPhone { get; set; }

    public string? TenantEmail { get; set; }

    public string? TenantSex { get; set; }

    public string? LandlordFullName { get; set; }

    public string? LandlordNationalIdCardNumber { get; set; }

    public string? LandlordPhone { get; set; }

    public string ApartmentTitle { get; set; } = null!;

    public string? ApartmentAddress { get; set; }

    public string? ApartmentDistrict { get; set; }

    public string? ApartmentCity { get; set; }

    public DateOnly CheckInDate { get; set; }

    public DateOnly CheckOutDate { get; set; }

    public bool? ReportedToPolice { get; set; }

    public DateOnly? ReportDate { get; set; }

    public string? ReportNumber { get; set; }

    public int OccupantCount { get; set; }

    public List<ResidenceReportOccupantDto> Occupants { get; set; } = new();
}

public class ResidenceReportOccupantDto
{
    public int Order { get; set; }

    public bool IsPrimary { get; set; }

    public string? FullName { get; set; }

    public string? PassportId { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public string? NationalIdCardNumber { get; set; }

    public string? Nationality { get; set; }

    public string? Sex { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? ProofPhotoUrl { get; set; }
}

public class AddBookingOccupantDto
{
    [MaxLength(150)]
    public string? FullName { get; set; }

    [MaxLength(50)]
    public string? PassportId { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [MaxLength(20)]
    public string? NationalIdCardNumber { get; set; }

    [MaxLength(2)]
    public string? Nationality { get; set; }

    [MaxLength(20)]
    public string? Sex { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(255)]
    public string? Email { get; set; }

    [MaxLength(1000)]
    public string? ProofPhotoUrl { get; set; }
}

public class AddBookingOccupantFormDto
{
    [MaxLength(150)]
    public string? FullName { get; set; }

    [MaxLength(50)]
    public string? PassportId { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [MaxLength(20)]
    public string? NationalIdCardNumber { get; set; }

    [MaxLength(2)]
    public string? Nationality { get; set; }

    [MaxLength(20)]
    public string? Sex { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(255)]
    public string? Email { get; set; }

    [Required]
    public IFormFile ProofPhoto { get; set; } = null!;
}

public class FillBookingOccupantItemDto
{
    [Required]
    public int OccupantOrder { get; set; }

    public bool IsPrimary { get; set; }

    [MaxLength(150)]
    public string? FullName { get; set; }

    [MaxLength(50)]
    public string? PassportId { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [MaxLength(20)]
    public string? NationalIdCardNumber { get; set; }

    [MaxLength(2)]
    public string? Nationality { get; set; }

    [MaxLength(20)]
    public string? Sex { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(255)]
    public string? Email { get; set; }

    [MaxLength(1000)]
    public string? ProofPhotoUrl { get; set; }
}

public class UpdateBookingOccupantDto
{
    public bool? IsPrimary { get; set; }

    [MaxLength(150)]
    public string? FullName { get; set; }

    [MaxLength(50)]
    public string? PassportId { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [MaxLength(20)]
    public string? NationalIdCardNumber { get; set; }

    [MaxLength(2)]
    public string? Nationality { get; set; }

    [MaxLength(20)]
    public string? Sex { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(255)]
    public string? Email { get; set; }

    [MaxLength(1000)]
    public string? ProofPhotoUrl { get; set; }
}

public class UpdateBookingOccupantFormDto
{
    public bool? IsPrimary { get; set; }

    [MaxLength(150)]
    public string? FullName { get; set; }

    [MaxLength(50)]
    public string? PassportId { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [MaxLength(20)]
    public string? NationalIdCardNumber { get; set; }

    [MaxLength(2)]
    public string? Nationality { get; set; }

    [MaxLength(20)]
    public string? Sex { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(255)]
    public string? Email { get; set; }

    public IFormFile? ProofPhoto { get; set; }
}

public class FillBookingOccupantsDto
{
    [Required]
    [MinLength(1)]
    public List<FillBookingOccupantItemDto> Occupants { get; set; } = new();
}

public class BookingOccupantOcrUploadDto
{
    [Required]
    public IFormFile Image { get; set; } = null!;
}

/// <summary>
/// DTO representing a booking that has been reported by tenant or has disputes.
/// </summary>
public class ReportedBookingDto
{
    public Guid BookingId { get; set; }
    public Guid TenantId { get; set; }
    public string? TenantFullName { get; set; }
    public Guid ApartmentId { get; set; }
    public string? ApartmentAddress { get; set; }
    public Guid LandlordId { get; set; }
    public string? LandlordFullName { get; set; }

    public DateOnly CheckInDate { get; set; }
    public DateOnly CheckOutDate { get; set; }
    public int Nights { get; set; }
    public decimal TotalPrice { get; set; }

    public string? BookingStatus { get; set; }

    /// <summary>
    /// Whether there's a check-time dispute (disputed status).
    /// </summary>
    public bool HasCheckTimeDispute { get; set; }

    public string? DisputeReason { get; set; }
    public string? DisputeResolutionStatus { get; set; }
    public DateTime? DisputeCreatedAt { get; set; }

    /// <summary>
    /// Number of support tickets created for this booking by the tenant.
    /// </summary>
    public int SupportTicketCount { get; set; }

    /// <summary>
    /// If there's a related support ticket for this reported booking, the latest ticket ID.
    /// </summary>
    public Guid? TicketId { get; set; }

    /// <summary>
    /// URLs of images/attachments associated with the related support ticket (if any).
    /// </summary>
    public List<string> Images { get; set; } = new();
    public List<string> CheckTimeImages { get; set; } = new();

    public DateTime? CreatedAt { get; set; }
}

