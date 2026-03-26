using System;
using System.Linq;
using System.Threading.Tasks;
using BLL.Services.Interfaces;
using Common.DTOs;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements
{
    public class IdentityVerificationService : IIdentityVerificationService
    {
        private static readonly string[] VietnameseAllowedDocumentTypes =
        {
            "national_id_card", "passport", "drivers_license", "other_government_id"
        };

        private static readonly string[] ForeignAllowedDocumentTypes =
        {
            "passport", "other_government_id"
        };

        private readonly IRepository<User> _userRepository;
        private readonly IRepository<Tenant> _tenantRepository;
        private readonly IRepository<UserIdentityDocument> _userIdentityDocumentRepository;

        public IdentityVerificationService(
            IRepository<User> userRepository,
            IRepository<Tenant> tenantRepository,
            IRepository<UserIdentityDocument> userIdentityDocumentRepository)
        {
            _userRepository = userRepository;
            _tenantRepository = tenantRepository;
            _userIdentityDocumentRepository = userIdentityDocumentRepository;
        }

        public async Task<IdentityDocumentDto[]> GetUserDocumentsAsync(Guid userId)
        {
            var documents = await _userIdentityDocumentRepository.FindAsync(d => d.UserId == userId);

            return documents
                .Select(d => new IdentityDocumentDto
                {
                    DocumentId = d.DocumentId,
                    DocumentType = d.DocumentType,
                    Side = d.Side,
                    FileUrl = d.FileUrl,
                    MimeType = d.MimeType,
                    FileSize = d.FileSize,
                    UploadedAt = d.UploadedAt,
                    VerificationStatus = d.VerificationStatus,
                    VerifiedAt = d.VerifiedAt,
                    RejectionReason = d.RejectionReason,
                    Notes = d.Notes
                })
                .ToArray();
        }

        public async Task<Guid> AddIdentityDocumentAsync(Guid userId, IdentityDocumentUploadDto dto)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new ArgumentException("User not found.");
            }

            var document = new UserIdentityDocument
            {
                DocumentId = Guid.NewGuid(),
                UserId = userId,
                DocumentType = dto.DocumentType.Trim().ToLowerInvariant(),
                Side = string.IsNullOrWhiteSpace(dto.Side) ? null : dto.Side.Trim().ToLowerInvariant(),
                FileUrl = dto.FileUrl,
                FileKey = dto.FileKey,
                MimeType = dto.MimeType,
                FileSize = dto.FileSize,
                Notes = dto.Notes,
                UploadedAt = DateTime.UtcNow
            };

            await _userIdentityDocumentRepository.AddAsync(document);
            await _userIdentityDocumentRepository.SaveChangesAsync();

            return document.DocumentId;
        }

        public async Task ReviewIdentityDocumentAsync(ReviewIdentityDocumentDto dto)
        {
            var document = await _userIdentityDocumentRepository.GetByIdAsync(dto.DocumentId);
            if (document == null)
            {
                throw new ArgumentException("Identity document not found.");
            }

            if (dto.Approved)
            {
                document.VerificationStatus = "verified";
                document.VerifiedAt = DateTime.UtcNow;
                document.RejectionReason = null;
            }
            else
            {
                document.VerificationStatus = "rejected";
                document.VerifiedAt = null;
                document.RejectionReason = dto.RejectionReason;
            }

            _userIdentityDocumentRepository.Update(document);
            await _userIdentityDocumentRepository.SaveChangesAsync();
        }

        public async Task EnsureUserVerifiedForBookingAsync(Guid userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new ArgumentException("User not found.");
            }

            // For now, enforce rules only for tenant bookings.
            if (!string.Equals(user.Role, "tenant", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var tenant = await _tenantRepository.GetByIdAsync(userId);
            if (tenant == null)
            {
                throw new InvalidOperationException("Tenant profile is required before booking.");
            }

            if (string.IsNullOrWhiteSpace(tenant.Nationality))
            {
                throw new InvalidOperationException("Tenant nationality is required before booking.");
            }

            var isVietnamese = string.Equals(tenant.Nationality, "VN", StringComparison.OrdinalIgnoreCase);

            var documents = await _userIdentityDocumentRepository.FindAsync(d =>
                d.UserId == userId &&
                d.VerificationStatus != null &&
                d.VerificationStatus == "verified");

            bool HasAllowedType(UserIdentityDocument doc, string[] allowedTypes)
            {
                return allowedTypes.Any(t => string.Equals(doc.DocumentType, t, StringComparison.OrdinalIgnoreCase));
            }

            var allowedTypes = isVietnamese ? VietnameseAllowedDocumentTypes : ForeignAllowedDocumentTypes;

            var hasRequiredDocument = documents.Any(d => HasAllowedType(d, allowedTypes));

            if (!hasRequiredDocument)
            {
                var message = isVietnamese
                    ? "Vietnamese tenants must verify identity with a national ID card or other government-issued ID (passport, driver's license, or similar) before booking."
                    : "Foreign tenants must verify identity with a passport or other government-issued ID before booking.";
                throw new InvalidOperationException(message);
            }

            // If already marked verified, nothing more to do.
            if (user.IdentityVerified == true &&
                string.Equals(tenant.IdentityVerificationStatus, "verified", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            user.IdentityVerified = true;
            tenant.IdentityVerificationStatus = "verified";
            tenant.LastVerifiedAt = DateTime.UtcNow;

            _userRepository.Update(user);
            _tenantRepository.Update(tenant);

            await _userRepository.SaveChangesAsync();
            await _tenantRepository.SaveChangesAsync();
        }
    }
}
