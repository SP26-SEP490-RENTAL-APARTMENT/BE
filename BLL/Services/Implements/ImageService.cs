using BLL.Services.Interfaces;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Common.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace BLL.Services.Implements
{
    public class ImageService : IImageService
    {
        private const long MaxUploadSizeBytes = 25 * 1024 * 1024;
        private const long MaxVideoUploadSizeBytes = 100 * 1024 * 1024; // 100 MB


        private static readonly HashSet<string> AllowedVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".mov", ".avi", ".webm", ".mkv", ".flv", ".wmv"
        };

        private static readonly HashSet<string> AllowedVideoContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "video/mp4",
            "video/quicktime",
            "video/x-msvideo",
            "video/webm",
            "video/x-matroska",
            "video/x-flv",
            "video/x-ms-wmv"
        };

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

        private readonly Cloudinary _cloudinary;

        public ImageService(IOptions<CloudinarySettings> config)
        {
            var acc = new Account(
                config.Value.CloudName,
                config.Value.ApiKey,
                config.Value.ApiSecret
            );

            _cloudinary = new Cloudinary(acc);
        }

        public async Task<string> UploadImageAsync(IFormFile file)
        {
            if (file == null)
            {
                throw new ArgumentException("File is required.");
            }

            if (file.Length <= 0)
            {
                throw new ArgumentException("File cannot be empty.");
            }

            if (file.Length > MaxUploadSizeBytes)
            {
                throw new ArgumentException("File size exceeds the maximum allowed limit of 25 MB.");
            }

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            {
                throw new ArgumentException("Unsupported file type. Allowed types are image files only (jpg, jpeg, png, gif, webp, bmp).");
            }

            if (!string.IsNullOrWhiteSpace(file.ContentType) && !AllowedContentTypes.Contains(file.ContentType))
            {
                throw new ArgumentException("Unsupported file content type.");
            }

            var uploadResult = new ImageUploadResult();

            using var stream = file.OpenReadStream();
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
            };

            uploadResult = await _cloudinary.UploadAsync(uploadParams);

            return uploadResult.SecureUrl?.AbsoluteUri ?? string.Empty;
        }

        public async Task<string> UploadMediaAsync(IFormFile file)
        {
            if (file == null)
                throw new ArgumentException("File is required.");

            if (file.Length <= 0)
                throw new ArgumentException("File cannot be empty.");

            if (file.Length > MaxVideoUploadSizeBytes)
                throw new ArgumentException($"File size exceeds the maximum allowed limit of {MaxVideoUploadSizeBytes / (1024 * 1024)} MB.");

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension) || !AllowedVideoExtensions.Contains(extension))
                throw new ArgumentException("Unsupported video type. Allowed types: mp4, mov, avi, webm, mkv, flv, wmv.");

            if (!string.IsNullOrWhiteSpace(file.ContentType) && !AllowedVideoContentTypes.Contains(file.ContentType))
                throw new ArgumentException("Unsupported video content type.");

            using var stream = file.OpenReadStream();
            var uploadParams = new VideoUploadParams
            {
                File = new FileDescription(file.FileName, stream)
                // Optionally add eager transformations, folder, etc.
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            return uploadResult.SecureUrl?.AbsoluteUri ?? string.Empty;
        }

        public static bool IsImage(IFormFile file)
        {
            var ext = Path.GetExtension(file.FileName);
            return !string.IsNullOrEmpty(ext) && AllowedExtensions.Contains(ext);
        }

        public static bool IsVideo(IFormFile file)
        {
            var ext = Path.GetExtension(file.FileName);
            return !string.IsNullOrEmpty(ext) && AllowedVideoExtensions.Contains(ext);
        }
    }
}
