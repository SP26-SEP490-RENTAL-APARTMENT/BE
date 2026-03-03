using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class HolidaysEvent
{
    public Guid EventId { get; set; }

    public string EventName { get; set; } = null!;

    public string EventType { get; set; } = null!;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public string? LocationScope { get; set; }

    public string? Description { get; set; }

    public bool? IsRecurring { get; set; }

    public string? RecurrenceRule { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
