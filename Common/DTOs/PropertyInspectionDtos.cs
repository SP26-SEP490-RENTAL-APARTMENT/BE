using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Common.DTOs
{
    public class PropertyInspectionRequestDto : IValidatableObject
    {
        [Required]
        public Guid InspectorId { get; set; }

        public DateOnly? ScheduledDate { get; set; }

        [MaxLength(50)]
        public string? Status { get; set; }

        public string? OverallCondition { get; set; }

        public string? IssuesFound { get; set; }

        public string? Recommendations { get; set; }

        public bool? ApprovedForListing { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            if (ScheduledDate.HasValue && ScheduledDate.Value < today)
            {
                yield return new ValidationResult("Scheduled date cannot be earlier than today.", new[] { nameof(ScheduledDate) });
            }

            // Allowed statuses: pending, scheduled, in_progress, passed, failed, re_inspection_needed
            if (!string.IsNullOrWhiteSpace(Status))
            {
                HashSet<string> allowed = new(StringComparer.OrdinalIgnoreCase)
                {
                    "pending",
                    "scheduled",
                    "in_progress",
                    "passed",
                    "failed",
                    "re_inspection_needed"
                };

                if (!allowed.Contains(Status))
                {
                    yield return new ValidationResult($"Status must be one of: {string.Join(", ", allowed)}.", new[] { nameof(Status) });
                }
            }
        }
    }

    public class CreatePropertyInspectionDto : IValidatableObject
    {
        [Required]
        public Guid ApartmentId { get; set; }

        [Required]
        public Guid InspectorId { get; set; }

        public DateOnly? ScheduledDate { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            if (ScheduledDate.HasValue && ScheduledDate.Value < today)
            {
                yield return new ValidationResult("Scheduled date cannot be earlier than today.", new[] { nameof(ScheduledDate) });
            }
        }
    }

    public class PropertyInspectionResponseDto
    {
        public Guid InspectionId { get; set; }
        public Guid ApartmentId { get; set; }
        public Guid InspectorId { get; set; }
        public DateOnly? ScheduledDate { get; set; }
        public DateOnly? CompletedDate { get; set; }
        public string? Status { get; set; }
        public string? OverallCondition { get; set; }
        public string? IssuesFound { get; set; }
        public string? Recommendations { get; set; }
        public bool? ApprovedForListing { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public Guid? ApprovedBy { get; set; }
    }

    public class InspectionPhotoCreateDto
    {
        [Required]
        [MaxLength(500)]
        public string FileUrl { get; set; } = null!;

        [MaxLength(255)]
        public string? FileKey { get; set; }

        [MaxLength(200)]
        public string? Description { get; set; }

        public bool? IsIssue { get; set; }
    }

    public class CompletePropertyInspectionDto
    {
        public string? OverallCondition { get; set; }

        public string? IssuesFound { get; set; }

        public string? Recommendations { get; set; }

        public List<InspectionPhotoCreateDto> Photos { get; set; } = new();
    }

    public class CancelPropertyInspectionDto
    {
        [Required]
        [MaxLength(1000)]
        public string Reason { get; set; } = null!;
    }

    public class ReviewPropertyInspectionDto : IValidatableObject
    {
        [Required]
        [RegularExpression("^(approve|reject)$", ErrorMessage = "Decision must be 'approve' or 'reject'.")]
        public string Decision { get; set; } = null!;

        [MaxLength(1000)]
        public string? Reason { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.Equals(Decision, "reject", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(Reason))
            {
                yield return new ValidationResult("Reason is required when rejecting an inspection.", new[] { nameof(Reason) });
            }
        }
    }
}