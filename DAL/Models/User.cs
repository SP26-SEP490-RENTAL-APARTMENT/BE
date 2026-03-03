using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class User
{
    public Guid UserId { get; set; }

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string Role { get; set; } = null!;

    public string? FullName { get; set; }

    public string? Phone { get; set; }

    public bool? IdentityVerified { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<AdminAction> AdminActions { get; set; } = new List<AdminAction>();

    public virtual Landlord? Landlord { get; set; }

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<PropertyInspection> PropertyInspectionApprovedByNavigations { get; set; } = new List<PropertyInspection>();

    public virtual ICollection<PropertyInspection> PropertyInspectionInspectors { get; set; } = new List<PropertyInspection>();

    public virtual ICollection<Review> ReviewRevieweds { get; set; } = new List<Review>();

    public virtual ICollection<Review> ReviewReviewers { get; set; } = new List<Review>();

    public virtual ICollection<SupportTicketAssignment> SupportTicketAssignments { get; set; } = new List<SupportTicketAssignment>();

    public virtual ICollection<SupportTicket> SupportTicketResolvedByNavigations { get; set; } = new List<SupportTicket>();

    public virtual ICollection<SupportTicket> SupportTicketUsers { get; set; } = new List<SupportTicket>();

    public virtual Tenant? Tenant { get; set; }

    public virtual ICollection<UserIdentityDocument> UserIdentityDocuments { get; set; } = new List<UserIdentityDocument>();
}
