using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Common.DTOs
{
    public class IdentityDocumentUploadDto
    {
        [Required]
        [RegularExpression("^(passport|national_id_card|drivers_license|other_government_id|selfie_with_id)$",
            ErrorMessage = "Invalid document type.")]
        public string DocumentType { get; set; } = null!;

        [RegularExpression("^(front|back|bio_page|other)$",
            ErrorMessage = "Invalid document side.")]
        public string? Side { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "At least one file is required.")]
        public List<IFormFile> Files { get; set; } = new();

        public string? Notes { get; set; }
    }

    public class ReviewIdentityDocumentDto
    {
        [Required]
        public Guid DocumentId { get; set; }

        [Required]
        public bool Approved { get; set; }

        public string? RejectionReason { get; set; }
    }

    public class IdentityDocumentDto
    {
        public Guid DocumentId { get; set; }
        public Guid UserId { get; set; }
        public string DocumentType { get; set; } = null!;
        public string? Side { get; set; }
        public string FileUrl { get; set; } = null!;
        public string? MimeType { get; set; }
        public long? FileSize { get; set; }
        public DateTime? UploadedAt { get; set; }
        public string? VerificationStatus { get; set; }
        public DateTime? VerifiedAt { get; set; }
        public string? RejectionReason { get; set; }
        public string? Notes { get; set; }
    }
}
