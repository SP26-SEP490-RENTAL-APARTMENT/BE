using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class BookingCheckTime
{
    public Guid CheckTimeId { get; set; }

    public Guid BookingId { get; set; }

    public DateTime ScheduledCheckIn { get; set; }

    public DateTime ScheduledCheckOut { get; set; }

    public DateTime? ActualCheckIn { get; set; }

    public bool? IsLateCheckIn { get; set; }

    public DateTime? ActualCheckOut { get; set; }

    public bool? IsLateCheckOut { get; set; }

    public decimal? LateCheckOutFee { get; set; }

    public bool? IsEarlyCheckIn { get; set; }

    public decimal? EarlyCheckInFee { get; set; }

    public string? FeeSettlementStatus { get; set; }

    public DateTime? FeeDueAt { get; set; }

    public DateTime? FeeSettledAt { get; set; }

    public string? FeeSettlementNotes { get; set; }

    public bool? TempResidenceReported { get; set; }

    public DateTime? ReportedAt { get; set; }

    public string? ReportReference { get; set; }

    public Guid? RecordedBy { get; set; }

    public DateTime? RecordedAt { get; set; }

    public string? Notes { get; set; }

    public string? CheckInPhotoUrl { get; set; }
    public string? CheckOutPhotoUrl { get; set; }

    public DateTime? ClaimOpenedAt { get; set; }
    public DateTime? ClaimExpiresAt { get; set; }
    public DateTime? ClaimLockedAt { get; set; }
    public string? ClaimStatus { get; set; }

    public decimal? LandlordPendingCreditAmount { get; set; }

    public DateTime? LandlordFundsReleasedAt { get; set; }

    public string? NoShowStatus { get; set; }
    public Guid? NoShowMarkedBy { get; set; }
    public DateTime? NoShowMarkedAt { get; set; }
    public DateTime? NoShowEligibleAt { get; set; }
    public int? NoShowGraceHours { get; set; }
    public string? MissingCheckOutStatus { get; set; }
    public DateTime? AutoClosedAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? TenantResponseStatus { get; set; }

    public Guid? TenantRespondedBy { get; set; }

    public DateTime? TenantRespondedAt { get; set; }

    public string? TenantDisputeReason { get; set; }

    public string? TenantDisputeNotes { get; set; }

    public string? DisputeResolutionStatus { get; set; }

    public Guid? DisputeResolvedBy { get; set; }

    public DateTime? DisputeResolvedAt { get; set; }

    public string? DisputeResolutionNotes { get; set; }

    public virtual Booking Booking { get; set; } = null!;
}
