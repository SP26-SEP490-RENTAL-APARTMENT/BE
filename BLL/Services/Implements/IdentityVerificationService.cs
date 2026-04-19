using System;
using System.Collections.Generic;
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
        private static readonly string[] DocumentAllowedColumns =
        {
            "DocumentId",
            "UserId",
            "DocumentType",
            "Side",
            "MimeType",
            "FileSize",
            "UploadedAt",
            "VerifiedAt",
            "VerificationStatus"
        };

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
        private readonly IRepository<Landlord> _landlordRepository;
        private readonly IRepository<UserIdentityDocument> _userIdentityDocumentRepository;
        private readonly INotificationService _notificationService;
        private readonly IIdentityDocumentUploadService _identityDocumentUploadService;

        private const string IdentityDocumentReferenceType = "identity_document";
        private const string IdentityVerifiedNotificationType = "identity_verified";
        private const string IdentityRejectedNotificationType = "identity_rejected";

        public IdentityVerificationService(
            IRepository<User> userRepository,
            IRepository<Tenant> tenantRepository,
            IRepository<Landlord> landlordRepository,
            IRepository<UserIdentityDocument> userIdentityDocumentRepository,
            INotificationService notificationService,
            IIdentityDocumentUploadService identityDocumentUploadService)
        {
            _userRepository = userRepository;
            _tenantRepository = tenantRepository;
            _landlordRepository = landlordRepository;
            _userIdentityDocumentRepository = userIdentityDocumentRepository;
            _notificationService = notificationService;
            _identityDocumentUploadService = identityDocumentUploadService;
        }

        public async Task<(IEnumerable<IdentityDocumentDto> Items, int TotalCount)> GetUserDocumentsAsync(
            Guid userId,
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null)
        {
            var filters = new Dictionary<string, string>
            {
                ["UserId"] = userId.ToString()
            };

            var (documents, totalCount) = await _userIdentityDocumentRepository.GetAllAsync(
                page,
                pageSize,
                sortBy,
                sortOrder,
                null,
                filters,
                DocumentAllowedColumns);

            return (MapDocumentDtos(documents), totalCount);
        }

        public async Task<(IEnumerable<IdentityDocumentDto> Items, int TotalCount)> GetAllDocumentsAsync(
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null)
        {
            var (documents, totalCount) = await _userIdentityDocumentRepository.GetAllAsync(
                page,
                pageSize,
                sortBy,
                sortOrder,
                null,
                null,
                DocumentAllowedColumns);

            return (MapDocumentDtos(documents), totalCount);
        }

        private static IEnumerable<IdentityDocumentDto> MapDocumentDtos(IEnumerable<UserIdentityDocument> documents)
        {
            return documents
                .Select(d => new IdentityDocumentDto
                {
                    DocumentId = d.DocumentId,
                    DocumentType = d.DocumentType,
                    UserId = d.UserId,
                    Side = d.Side,
                    FileUrl = d.FileUrl,
                    MimeType = d.MimeType,
                    FileSize = d.FileSize,
                    UploadedAt = d.UploadedAt,
                    VerificationStatus = d.VerificationStatus,
                    VerifiedAt = d.VerifiedAt,
                    RejectionReason = d.RejectionReason,
                    Notes = d.Notes
                });
        }

        public async Task<Guid[]> AddIdentityDocumentAsync(Guid userId, IdentityDocumentUploadDto dto)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new ArgumentException("User not found.");
            }

            Tenant? tenant = null;
            Landlord? landlord = null;
            
            if (string.Equals(user.Role, "tenant", StringComparison.OrdinalIgnoreCase))
            {
                tenant = await _tenantRepository.GetByIdAsync(userId);
            }
            else if (string.Equals(user.Role, "landlord", StringComparison.OrdinalIgnoreCase))
            {
                landlord = await _landlordRepository.GetByIdAsync(userId);
            }

            if (dto.Files == null || dto.Files.Count == 0)
            {
                throw new ArgumentException("At least one identity document file is required.");
            }

            var createdDocumentIds = new List<Guid>();

            foreach (var file in dto.Files)
            {
                if (file == null || file.Length == 0)
                {
                    throw new ArgumentException("Each identity document file must be non-empty.");
                }

                var fileUrl = await _identityDocumentUploadService.UploadIdentityDocumentAsync(file);

                var document = new UserIdentityDocument
                {
                    DocumentId = Guid.NewGuid(),
                    UserId = userId,
                    DocumentType = dto.DocumentType.Trim().ToLowerInvariant(),
                    Side = string.IsNullOrWhiteSpace(dto.Side) ? null : dto.Side.Trim().ToLowerInvariant(),
                    FileUrl = fileUrl,
                    FileKey = null,
                    MimeType = file.ContentType,
                    FileSize = file.Length,
                    Notes = dto.Notes,
                    UploadedAt = Common.Utils.VietnamTime.Now
                };

                await _userIdentityDocumentRepository.AddAsync(document);
                createdDocumentIds.Add(document.DocumentId);
            }

            await _userIdentityDocumentRepository.SaveChangesAsync();

            if (tenant != null && !string.Equals(tenant.IdentityVerificationStatus, "verified", StringComparison.OrdinalIgnoreCase))
            {
                tenant.IdentityVerificationStatus = "pending";
                _tenantRepository.Update(tenant);
                await _tenantRepository.SaveChangesAsync();
            }
            else if (landlord != null && !string.Equals(landlord.IdentityVerificationStatus, "verified", StringComparison.OrdinalIgnoreCase))
            {
                landlord.IdentityVerificationStatus = "pending";
                _landlordRepository.Update(landlord);
                await _landlordRepository.SaveChangesAsync();
            }

            return createdDocumentIds.ToArray();
        }

        public async Task ReviewIdentityDocumentAsync(ReviewIdentityDocumentDto dto)
        {
            var document = await _userIdentityDocumentRepository.GetByIdAsync(dto.DocumentId);
            if (document == null)
            {
                throw new ArgumentException("Identity document not found.");
            }

            var user = await _userRepository.GetByIdAsync(document.UserId);
            if (user == null)
            {
                throw new ArgumentException("User not found.");
            }

            Tenant? tenant = null;
            Landlord? landlord = null;
            
            if (string.Equals(user.Role, "tenant", StringComparison.OrdinalIgnoreCase))
            {
                tenant = await _tenantRepository.GetByIdAsync(user.UserId);
            }
            else if (string.Equals(user.Role, "landlord", StringComparison.OrdinalIgnoreCase))
            {
                landlord = await _landlordRepository.GetByIdAsync(user.UserId);
            }

            if (dto.Approved)
            {
                document.VerificationStatus = "verified";
                document.VerifiedAt = Common.Utils.VietnamTime.Now;
                document.RejectionReason = null;
                user.IdentityVerified = true;

                if (tenant != null)
                {
                    tenant.IdentityVerificationStatus = "verified";
                    tenant.LastVerifiedAt = Common.Utils.VietnamTime.Now;
                }
                else if (landlord != null)
                {
                    landlord.IdentityVerificationStatus = "verified";
                    landlord.LastVerifiedAt = Common.Utils.VietnamTime.Now;
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(dto.RejectionReason))
                {
                    throw new ArgumentException("Rejection reason is required when rejecting an identity document.");
                }

                document.VerificationStatus = "rejected";
                document.VerifiedAt = null;
                document.RejectionReason = dto.RejectionReason;

                var otherVerifiedDocuments = await _userIdentityDocumentRepository.FindAsync(d =>
                    d.UserId == document.UserId &&
                    d.DocumentId != document.DocumentId &&
                    d.VerificationStatus != null &&
                    d.VerificationStatus == "verified");

                user.IdentityVerified = otherVerifiedDocuments.Any();

                if (tenant != null)
                {
                    tenant.IdentityVerificationStatus = user.IdentityVerified == true ? "verified" : "rejected";
                    if (user.IdentityVerified != true)
                    {
                        tenant.LastVerifiedAt = null;
                    }
                }
                else if (landlord != null)
                {
                    landlord.IdentityVerificationStatus = user.IdentityVerified == true ? "verified" : "rejected";
                    if (user.IdentityVerified != true)
                    {
                        landlord.LastVerifiedAt = null;
                    }
                }
            }

            _userIdentityDocumentRepository.Update(document);
            _userRepository.Update(user);
            if (tenant != null)
            {
                _tenantRepository.Update(tenant);
            }
            else if (landlord != null)
            {
                _landlordRepository.Update(landlord);
            }

            await _userIdentityDocumentRepository.SaveChangesAsync();
            await _userRepository.SaveChangesAsync();
            if (tenant != null)
            {
                await _tenantRepository.SaveChangesAsync();
            }
            else if (landlord != null)
            {
                await _landlordRepository.SaveChangesAsync();
            }
            await CreateReviewNotificationAsync(document.UserId, document.DocumentId, dto.Approved, dto.RejectionReason);
        }

        public async Task EnsureUserVerifiedForInspectionAsync(Guid landlordId)
        {
            var user = await _userRepository.GetByIdAsync(landlordId);
            if (user == null)
            {
                throw new ArgumentException("User not found.");
            }

            if (!string.Equals(user.Role, "landlord", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var landlord = await _landlordRepository.GetByIdAsync(landlordId);
            if (landlord == null)
            {
                throw new InvalidOperationException("Landlord profile is required.");
            }

            if (string.IsNullOrWhiteSpace(user.Nationality))
            {
                throw new InvalidOperationException("Landlord nationality is required before property inspection.");
            }

            var isVietnamese = string.Equals(user.Nationality, "VN", StringComparison.OrdinalIgnoreCase);

            var documents = await _userIdentityDocumentRepository.FindAsync(d =>
                d.UserId == landlordId &&
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
                    ? "Vietnamese landlords must verify identity with a national ID card or other government-issued ID (passport, driver's license, or similar) before property inspection."
                    : "Foreign landlords must verify identity with a passport or other government-issued ID before property inspection.";
                throw new InvalidOperationException(message);
            }

            if (user.IdentityVerified == true &&
                string.Equals(landlord.IdentityVerificationStatus, "verified", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            user.IdentityVerified = true;
            landlord.IdentityVerificationStatus = "verified";
            landlord.LastVerifiedAt = Common.Utils.VietnamTime.Now;

            _userRepository.Update(user);
            _landlordRepository.Update(landlord);

            await _userRepository.SaveChangesAsync();
            await _landlordRepository.SaveChangesAsync();
        }

        private async Task CreateReviewNotificationAsync(
            Guid userId,
            Guid documentId,
            bool approved,
            string? rejectionReason)
        {
            var notification = new DAL.Models.Notification
            {
                NotificationId = Guid.NewGuid(),
                UserId = userId,
                Type = approved ? IdentityVerifiedNotificationType : IdentityRejectedNotificationType,
                Title = approved ? "Identity verification approved" : "Identity verification rejected",
                Message = approved
                    ? "Your identity document has been approved."
                    : string.IsNullOrWhiteSpace(rejectionReason)
                        ? "Your identity document was rejected. Please upload a new document that meets the verification requirements."
                        : $"Your identity document was rejected: {rejectionReason}",
                ReferenceId = documentId,
                ReferenceType = IdentityDocumentReferenceType,
                IsRead = false,
                CreatedAt = Common.Utils.VietnamTime.Now
            };

            await _notificationService.CreateAsync(notification);
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

            if (string.IsNullOrWhiteSpace(user.Nationality))
            {
                throw new InvalidOperationException("Tenant nationality is required before booking.");
            }

            var isVietnamese = string.Equals(user.Nationality, "VN", StringComparison.OrdinalIgnoreCase);

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
            tenant.LastVerifiedAt = Common.Utils.VietnamTime.Now;

            _userRepository.Update(user);
            _tenantRepository.Update(tenant);

            await _userRepository.SaveChangesAsync();
            await _tenantRepository.SaveChangesAsync();
        }
    }
}
