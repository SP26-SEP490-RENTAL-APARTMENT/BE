using System.ComponentModel.DataAnnotations;

namespace Common.DTOs;

public class OccupiedRoomAlternativeOptionDto
{
    public Guid ApartmentId { get; set; }
    public string ApartmentTitle { get; set; } = null!;
    public decimal BasePricePerNight { get; set; }
    public decimal EstimatedTotalPrice { get; set; }
    public decimal PriceDifference { get; set; }
    public string AdjustmentType { get; set; } = null!;
    public ApartmentResponseDto Apartment { get; set; } = null!;
}

public class CreateBookingOfferRequestDto
{
    [Required]
    public Guid AlternativeApartmentId { get; set; }

    [MaxLength(100)]
    public string? Reason { get; set; }
}

public class ReportOccupiedIncidentRequestDto
{
    [Required]
    [MaxLength(2000)]
    public string Details { get; set; } = null!;
}

public class BookingOfferResponseDto
{
    public Guid OfferId { get; set; }
    public Guid OriginalBookingId { get; set; }
    public Guid AlternativeApartmentId { get; set; }
    public string Status { get; set; } = null!;
    public string? Reason { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal AlternativePrice { get; set; }
    public decimal PriceDifference { get; set; }
    public string AdjustmentType { get; set; } = null!;
    public bool ManualSettlementRequired { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public string? TenantResponseNotes { get; set; }
    public ApartmentResponseDto AlternativeApartment { get; set; } = null!;
}

public class RespondBookingOfferRequestDto
{
    [Required]
    public bool Accepted { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class ConfirmOccupiedIncidentPenaltyRequestDto
{
    public Guid? TicketId { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class LandlordPenaltyApplicationResultDto
{
    public decimal RequestedAmount { get; set; }
    public decimal DeductedFromAvailable { get; set; }
    public decimal DeductedFromPending { get; set; }
    public decimal DebtRecorded { get; set; }
}

public class ConfirmOccupiedIncidentPenaltyResponseDto
{
    public Guid BookingId { get; set; }
    public Guid? TicketId { get; set; }
    public decimal PenaltyAmount { get; set; }
    public bool AlreadyApplied { get; set; }
    public string Message { get; set; } = null!;
    public LandlordPenaltyApplicationResultDto Settlement { get; set; } = null!;
}
