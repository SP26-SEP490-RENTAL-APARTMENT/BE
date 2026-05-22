using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class SupportTicket
{
    public Guid TicketId { get; set; }

    public Guid UserId { get; set; }
    public Guid? BookingId { get; set; }
    public string Subject { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string Category { get; set; } = null!;

    public string? Priority { get; set; }

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public Guid? ResolvedBy { get; set; }

    public string? ResolutionNotes { get; set; }

    public virtual User? ResolvedByNavigation { get; set; }

    public virtual ICollection<SupportTicketAssignment> SupportTicketAssignments { get; set; } = new List<SupportTicketAssignment>();

    public virtual ICollection<SupportTicketAttachment> SupportTicketAttachments { get; set; } = new List<SupportTicketAttachment>();

    public virtual User User { get; set; } = null!;
    public virtual Booking? Booking { get; set; }
}
