using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Common.DTOs
{
    public class IdentityDocumentUploadDto
    {
        [Required]
        [RegularExpression("^(passport|national_id_card|drivers_license|other_government_id)$",
            ErrorMessage = "Invalid document type.")]
        public string DocumentType { get; set; } = null!;

        [RegularExpression("^(front|back|bio_page|other)$",
            ErrorMessage = "Invalid document side.")]
        public string? Side { get; set; }

        // Legacy bulk upload field.
        public List<IFormFile> Files { get; set; } = new();

        // Preferred explicit side-based fields, required for national_id_card.
        public IFormFile? FrontImage { get; set; }

        public IFormFile? BackImage { get; set; }

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

        public IdentityDocumentOcrSummaryDto? OcrSummary { get; set; }
    }

    public class IdentityDocumentOcrSummaryDto
    {
        public string Provider { get; set; } = string.Empty;

        public int? ProviderErrorCode { get; set; }

        public string? ProviderErrorMessage { get; set; }

        public string? CardType { get; set; }

        public string? CardTypeDetail { get; set; }

        public string? IdNumber { get; set; }

        public string? FullName { get; set; }

        public string? DateOfBirthRaw { get; set; }

        public string? IssueDateRaw { get; set; }

        public decimal? OverallConfidence { get; set; }

        public bool? AutoApproved { get; set; }

        public bool? MatchPassed { get; set; }

        public string? MatchFailureReason { get; set; }

        public DateTime? ProcessedAt { get; set; }
    }
}
