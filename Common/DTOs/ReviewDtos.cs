using System;
using System.ComponentModel.DataAnnotations;

namespace Common.DTOs
{
    public class CreateReviewRequestDto : IValidatableObject
    {
        [Required]
        public Guid BookingId { get; set; }

        [Required]
        public Guid ReviewedId { get; set; }

        public Guid? ApartmentId { get; set; }

        [Required]
        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
        public sbyte Rating { get; set; }

        [MaxLength(2000)]
        public string? CommentEn { get; set; }

        [MaxLength(2000)]
        public string? CommentVi { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(CommentEn) && string.IsNullOrWhiteSpace(CommentVi))
            {
                yield return new ValidationResult("At least one comment (English or Vietnamese) must be provided.", new[] { nameof(CommentEn), nameof(CommentVi) });
            }
        }
    }

    public class UpdateReviewRequestDto : IValidatableObject
    {
        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
        public sbyte? Rating { get; set; }

        [MaxLength(2000)]
        public string? CommentEn { get; set; }

        [MaxLength(2000)]
        public string? CommentVi { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // Optional: You could validate that if both comments are empty strings "" it's invalid, 
            // but for an update DTO sometimes null just means "don't update".
            yield break;
        }
    }

    public class ReviewResponseDto
    {
        public Guid ReviewId { get; set; }
        public Guid BookingId { get; set; }
        public Guid ReviewerId { get; set; }
        public Guid ReviewedId { get; set; }
        public Guid? ApartmentId { get; set; }
        public sbyte? Rating { get; set; }
        public string? CommentEn { get; set; }
        public string? CommentVi { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
