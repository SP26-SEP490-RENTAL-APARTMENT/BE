using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class UserIdentityDocument
{
    public Guid DocumentId { get; set; }

    public Guid UserId { get; set; }

    public string DocumentType { get; set; } = null!;

    public string? Side { get; set; }

    public string FileUrl { get; set; } = null!;

    public string? FileKey { get; set; }

    public string? MimeType { get; set; }

    public long? FileSize { get; set; }

    public DateTime? UploadedAt { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public string? VerificationStatus { get; set; }

    public string? RejectionReason { get; set; }

    public string? Notes { get; set; }

    public virtual User User { get; set; } = null!;
}
