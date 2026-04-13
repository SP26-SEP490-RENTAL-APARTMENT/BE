using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Common.Enums;

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

    [Range(0, 3, ErrorMessage = "Number of infants must be between 0 and 3.")]
    public int? NoOfInfants { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Number of pets cannot be negative.")]
    public int? NoOfPets { get; set; }

    public Guid? PackageId { get; set; }

    public BookingPaymentMode PaymentMode { get; set; } = BookingPaymentMode.partial;

    [MaxLength(20)]
    public string? PaymentProvider { get; set; }

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
    public DateOnly CheckInDate { get; set; }
    public DateOnly CheckOutDate { get; set; }
    public int Nights { get; set; }
    public int? NoOfAdults { get; set; }
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

public class CreateBookingResponseDto
{
    public BookingResponseDto Booking { get; set; } = null!;
    public BookingPaymentLinkDto? PaymentLink { get; set; }
}

public class BookingQuoteRequestDto : IValidatableObject
{
    [Required]
    public Guid ApartmentId { get; set; }

    public Guid? PackageId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "At least 1 adult is required.")]
    public int? NoOfAdults { get; set; }

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
    public decimal BaseAmount { get; set; }
    public decimal PackageAmount { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal SuggestedDeposit { get; set; }
    public decimal RemainingBalance { get; set; }
    public decimal FullUpfrontPaymentAmount { get; set; }
    public decimal FullUpfrontLandlordShareAmount { get; set; }
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

    public string TenantNationality { get; set; } = null!;

    public string? TenantPhone { get; set; }

    public string? TenantEmail { get; set; }

    public string? TenantSex { get; set; }

    public string? LandlordFullName { get; set; }

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
}
