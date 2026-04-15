using System;
using System.ComponentModel.DataAnnotations;

namespace Common.DTOs
{
    public class CreateReviewRequestDto
    {
        [Required]
        public Guid BookingId { get; set; }

        [Required]
        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
        public sbyte Rating { get; set; }

        [MaxLength(2000)]
        public string? Comment { get; set; }
    }

    public class UpdateReviewRequestDto
    {
        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
        public sbyte? Rating { get; set; }

        [MaxLength(2000)]
        public string? Comment { get; set; }
    }

    public class ReviewResponseDto
    {
        public Guid ReviewId { get; set; }
        public Guid BookingId { get; set; }
        public Guid TeanantId { get; set; }
        public string? TenantName { get; set; }
        public Guid LandlordId { get; set; }
        public string? LandlordName { get; set; }
        public Guid? ApartmentId { get; set; }
        public sbyte? Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
