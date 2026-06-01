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
        private const long MaxVideoUploadSizeBytes = 100 * 1024 * 1024;

        private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp"
        };

        private static readonly HashSet<string> AllowedVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".mov", ".avi", ".mkv", ".webm"
        };

        private static readonly HashSet<string> AllowedImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/gif",
            "image/webp",
            "image/bmp"
        };

        private static readonly HashSet<string> AllowedVideoContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "video/mp4",
            "video/quicktime",
            "video/x-msvideo",
            "video/x-matroska",
            "video/webm"
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

        public async Task DeleteImageAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            // Extract public_id from Cloudinary URL: .../upload/v{version}/{public_id}.{ext}
            var uploadIndex = url.IndexOf("/upload/", StringComparison.OrdinalIgnoreCase);
            if (uploadIndex < 0)
                return;

            var afterUpload = url.Substring(uploadIndex + "/upload/".Length);

            // Skip version segment (v1234567890/)
            if (afterUpload.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                var slashIndex = afterUpload.IndexOf('/');
                if (slashIndex >= 0)
                    afterUpload = afterUpload.Substring(slashIndex + 1);
            }

            // Strip file extension
            var dotIndex = afterUpload.LastIndexOf('.');
            var publicId = dotIndex >= 0 ? afterUpload.Substring(0, dotIndex) : afterUpload;

            if (string.IsNullOrWhiteSpace(publicId))
                return;

            var deleteParams = new DeletionParams(publicId);
            await _cloudinary.DestroyAsync(deleteParams);
        }

        public async Task<string> UploadImageAsync(IFormFile file)
        {
            if (file == null)
                throw new ArgumentException("File is required.");

            if (file.Length <= 0)
                throw new ArgumentException("File cannot be empty.");

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension))
                throw new ArgumentException("File has no extension.");

            bool isVideo = AllowedVideoExtensions.Contains(extension);
            bool isImage = AllowedImageExtensions.Contains(extension);

            if (!isImage && !isVideo)
                throw new ArgumentException("Unsupported file type. Allowed types: jpg, jpeg, png, gif, webp, bmp, mp4, mov, avi, mkv, webm.");

            if (isVideo)
            {
                if (file.Length > MaxVideoUploadSizeBytes)
                    throw new ArgumentException("Video size exceeds the maximum allowed limit of 100 MB.");

                if (!string.IsNullOrWhiteSpace(file.ContentType) && !AllowedVideoContentTypes.Contains(file.ContentType))
                    throw new ArgumentException("Unsupported video content type.");

                using var stream = file.OpenReadStream();
                var uploadParams = new VideoUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                };
                var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                return uploadResult.SecureUrl?.AbsoluteUri ?? string.Empty;
            }
            else
            {
                if (file.Length > MaxUploadSizeBytes)
                    throw new ArgumentException("File size exceeds the maximum allowed limit of 25 MB.");

                if (!string.IsNullOrWhiteSpace(file.ContentType) && !AllowedImageContentTypes.Contains(file.ContentType))
                    throw new ArgumentException("Unsupported image content type.");

                using var stream = file.OpenReadStream();
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                };
                var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                return uploadResult.SecureUrl?.AbsoluteUri ?? string.Empty;
            }
        }
    }
}
