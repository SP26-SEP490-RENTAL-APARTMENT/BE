using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Common.DTOs
{
    public class IdentityRecognitionUploadDto
    {
        [Required]
        public IFormFile Image { get; set; } = null!;
    }
}