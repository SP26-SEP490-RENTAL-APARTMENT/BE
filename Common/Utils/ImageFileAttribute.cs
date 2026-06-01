using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Common.Utils
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
    public sealed class ImageFileAttribute : ValidationAttribute
    {
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp"
        };

        private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/gif",
            "image/webp",
            "image/bmp"
        };

        public override bool IsValid(object? value)
        {
            if (value is not IFormFile file)
                return true;

            var ext = Path.GetExtension(file.FileName);
            return AllowedExtensions.Contains(ext) && AllowedContentTypes.Contains(file.ContentType);
        }

        public override string FormatErrorMessage(string name)
            => $"{name} must be an image file (.jpg, .jpeg, .png, .webp).";
    }
}
