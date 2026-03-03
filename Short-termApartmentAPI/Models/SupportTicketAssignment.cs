using System;
using System.Collections.Generic;

namespace Short_termApartmentAPI.Models;

public partial class SupportTicketAssignment
{
    public Guid AssignmentId { get; set; }

    public Guid TicketId { get; set; }

    public Guid StaffId { get; set; }

    public DateTime? AssignedAt { get; set; }

    public string? RoleInTicket { get; set; }

    public virtual User Staff { get; set; } = null!;

    public virtual SupportTicket Ticket { get; set; } = null!;
}
