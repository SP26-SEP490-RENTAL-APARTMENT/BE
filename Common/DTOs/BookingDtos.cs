using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Common.DTOs;

public class CreateBookingRequestDto : IValidatableObject
{

    [Required]
    public Guid ApartmentId { get; set; }

    [Required]
    public DateOnly CheckInDate { get; set; }

    [Required]
    public DateOnly CheckOutDate { get; set; }

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Nights must be at least 1.")]
    public int Nights { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "At least 1 adult is required.")]
    public int? NoOfAdults { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Number of infants cannot be negative.")]
    public int? NoOfInfants { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Number of pets cannot be negative.")]
    public int? NoOfPets { get; set; }

    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "Total price must be a non-negative value.")]
    public decimal TotalPrice { get; set; }

    public Guid? PackageId { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Package price must be a non-negative value.")]
    public decimal? PackagePrice { get; set; }

    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "Deposit amount must be a non-negative value.")]
    public decimal DepositAmount { get; set; }

    public bool? DepositPaid { get; set; }

    [Required]
    public DateOnly BalanceDueDate { get; set; }

    [MaxLength(50)]
    public string? Status { get; set; }

    [MaxLength(20)]
    public string? PaymentProvider { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (CheckInDate < today)
        {
            yield return new ValidationResult("Check-in date cannot be earlier than today.", new[] { nameof(CheckInDate) });
        }
        
        if (CheckInDate >= CheckOutDate)
        {
            yield return new ValidationResult("Check-out date must be later than check-in date.", new[] { nameof(CheckOutDate) });
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
        var today = DateOnly.FromDateTime(DateTime.Today);
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
    public bool? DepositPaid { get; set; }
    public DateOnly BalanceDueDate { get; set; }
    public string? Status { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class BookingQuoteRequestDto : IValidatableObject
{
    [Required]
    public Guid ApartmentId { get; set; }

    public Guid? PackageId { get; set; }

    [Required]
    public DateOnly CheckInDate { get; set; }

    [Required]
    public DateOnly CheckOutDate { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (CheckInDate < today)
        {
            yield return new ValidationResult("Check-in date cannot be earlier than today.", new[] { nameof(CheckInDate) });
        }

        if (CheckInDate >= CheckOutDate)
        {
            yield return new ValidationResult("Check-out date must be later than check-in date.", new[] { nameof(CheckOutDate) });
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
