using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Short_termApartmentAPI.DTOs
{
    public class RecordCheckInFormDto
    {
        [Required]
        public DateTime ActualCheckIn { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        [Required]
        public IFormFile PhotoEvidence { get; set; } = null!;
    }

    public class RecordCheckOutFormDto
    {
        [Required]
        public DateTime ActualCheckOut { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        [Required]
        public IFormFile PhotoEvidence { get; set; } = null!;
    }
}
