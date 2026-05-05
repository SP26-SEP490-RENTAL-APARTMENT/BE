using System;

namespace DAL.Models;

public partial class SupportTicketAttachment
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

    public virtual SupportTicket Ticket { get; set; } = null!;

    public virtual User UploadedByUser { get; set; } = null!;
}
