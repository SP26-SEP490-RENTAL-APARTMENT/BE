using System;

namespace DAL.Models;

public partial class BookingCheckTimeStateEvent
{
    public Guid EventId { get; set; }

    public Guid BookingId { get; set; }

    public Guid? CheckTimeId { get; set; }

    public string EventType { get; set; } = null!;

    public string? EventData { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }
}
