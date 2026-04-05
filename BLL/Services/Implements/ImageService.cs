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

        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp",
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx"
        };

        private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/gif",
            "image/webp",
            "image/bmp",
            "application/pdf",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.ms-excel",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "application/vnd.ms-powerpoint",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation"
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
                throw new ArgumentException("Unsupported file type. Allowed types are images, PDF, and common office documents.");
            }

            if (!string.IsNullOrWhiteSpace(file.ContentType) && !AllowedContentTypes.Contains(file.ContentType))
            {
                throw new ArgumentException("Unsupported file content type.");
            }

            var uploadResult = new RawUploadResult();

            using var stream = file.OpenReadStream();
            var uploadParams = new RawUploadParams
            {
                File = new FileDescription(file.FileName, stream),
            };

            uploadResult = await _cloudinary.UploadAsync(uploadParams);

            return uploadResult.SecureUrl?.AbsoluteUri ?? string.Empty;
        }
    }
}
