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

    public DateTime? ActualCheckOut { get; set; }

    public bool? IsLateCheckOut { get; set; }

    public decimal? LateCheckOutFee { get; set; }

    public bool? IsEarlyCheckIn { get; set; }

    public decimal? EarlyCheckInFee { get; set; }

    public bool? TempResidenceReported { get; set; }

    public DateTime? ReportedAt { get; set; }

    public string? ReportReference { get; set; }

    public Guid? RecordedBy { get; set; }

    public DateTime? RecordedAt { get; set; }

    public string? Notes { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Booking Booking { get; set; } = null!;
}
