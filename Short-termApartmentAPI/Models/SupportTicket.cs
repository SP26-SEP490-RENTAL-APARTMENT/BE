using System;
using System.Collections.Generic;

namespace Short_termApartmentAPI.Models;

public partial class SupportTicket
{
    public Guid TicketId { get; set; }

    public Guid UserId { get; set; }

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

    public virtual User User { get; set; } = null!;
}
