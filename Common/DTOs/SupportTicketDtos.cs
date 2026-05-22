using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Common.DTOs
{
    public class SupportTicketDto
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
        public ICollection<SupportTicketAttachmentDto> Attachments { get; set; } = new List<SupportTicketAttachmentDto>();
    }

    public class SupportTicketAttachmentDto
    {
        public Guid AttachmentId { get; set; }
        public Guid TicketId { get; set; }
        public string FileUrl { get; set; } = null!;
        public string? MimeType { get; set; }
        public long? FileSize { get; set; }
        public DateTime? UploadedAt { get; set; }
        public Guid UploadedBy { get; set; }
        public string? Caption { get; set; }
        public bool IsEvidence { get; set; }
    }

    public class UploadSupportTicketAttachmentDto
    {
        [Required]
        public List<IFormFile> Files { get; set; } = new();

        public string? Caption { get; set; }

        [Required]
        public bool IsEvidence { get; set; } = true;
    }


    public class CreateSupportTicketDto
    {
        [Required]
        [StringLength(255)]
        public string Subject { get; set; } = null!;

        [Required]
        public string Description { get; set; } = null!;

        [Required]

        [RegularExpression("^(booking_issue|payment_problem|listing_problem|account_verification|cancellation|dispute|property_quality|other)$", ErrorMessage = "Possible enum: 'booking_issue','payment_problem','listing_problem','account_verification','cancellation','dispute','property_quality','other'")]
        public string Category { get; set; } = null!;

        [Required]
        [RegularExpression("^(low|medium|high|urgent)$", ErrorMessage = "Role must be 'low, medium, high, urgent'")]
        public string? Priority { get; set; }
        public Guid? BookingId { get; set; }
        public List<IFormFile> Files { get; set; } = new();
    }

    public class UpdateSupportTicketDto
    {
        [Required]
        [StringLength(255)]
        public string Subject { get; set; } = null!;

        [Required]
        public string Description { get; set; } = null!;

        [Required]
        [RegularExpression("^(booking_issue|payment_problem|listing_problem|account_verification|cancellation|dispute|property_quality|other)$", ErrorMessage = "Possible enum: 'booking_issue','payment_problem','listing_problem','account_verification','cancellation','dispute','property_quality','other'")]
        public string Category { get; set; } = null!;

        [Required]
        [RegularExpression("^(low|medium|high|urgent)$", ErrorMessage = "Must be 'low, medium, high, urgent'")]
        public string? Priority { get; set; }

        [Required]
        [RegularExpression("^(open|in_progress|resolved|closed|escalated)$", ErrorMessage = "Must be 'open, in_progress, resolved, closed', 'escalated'")]
        public string? Status { get; set; }

        public string? ResolutionNotes { get; set; }

        public Guid? ResolvedBy { get; set; }

        public DateTime? ResolvedAt { get; set; }
    }

    public class ResolveTicketRequestDto
    {
        public Guid TicketId { get; set; }
        // Resolution notes are mandatory for resolution
        public string ResolutionNotes { get; set; } = string.Empty;
        // The ID of the user/staff member performing the resolution
        public Guid StaffActorUserId { get; set; }
    }

    public class UserUpdateStatusRequestDto
    {
        // Must be "closed" or "escalated"
        [Required]
        [RegularExpression("^(closed|escalated)$", ErrorMessage = "Must be 'closed', 'escalated'")]
        public string NewStatus { get; set; } = string.Empty;
        // Optional: Message/Notes if the status change requires documentation
        public string? StatusChangeNotes { get; set; }
    }
    public class ReportPersistingIssueDto
    {
        [Required]
        [StringLength(2000)]
        public string Details { get; set; } = null!;
    }
}