using System;
using System.ComponentModel.DataAnnotations;

namespace Common.DTOs;

public class CreateBookingRequestDto
{

    [Required]
    public Guid ApartmentId { get; set; }

    [Required]
    public DateOnly CheckInDate { get; set; }

    [Required]
    public DateOnly CheckOutDate { get; set; }

    [Required]
    public int Nights { get; set; }

    public int? NoOfAdults { get; set; }

    public int? NoOfInfants { get; set; }

    public int? NoOfPets { get; set; }

    [Required]
    public decimal TotalPrice { get; set; }

    public Guid? PackageId { get; set; }

    public decimal? PackagePrice { get; set; }

    [Required]
    public decimal DepositAmount { get; set; }

    public bool? DepositPaid { get; set; }

    [Required]
    public DateOnly BalanceDueDate { get; set; }

    public string? Status { get; set; }
}

public class UpdateBookingRequestDto
{
    public DateOnly? CheckInDate { get; set; }
    public DateOnly? CheckOutDate { get; set; }
    public int? Nights { get; set; }
    public int? NoOfAdults { get; set; }
    public int? NoOfInfants { get; set; }
    public int? NoOfPets { get; set; }
    public decimal? TotalPrice { get; set; }
    public Guid? PackageId { get; set; }
    public decimal? PackagePrice { get; set; }
    public decimal? DepositAmount { get; set; }
    public bool? DepositPaid { get; set; }
    public DateOnly? BalanceDueDate { get; set; }
    public string? Status { get; set; }
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
