using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BLL.Services.Interfaces;
using Common.DTOs;
using Common.Settings;
using DAL.Models;
using DAL.Repository.Interfaces;
using Microsoft.Extensions.Options;

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
        private readonly IRepository<IdentityDocumentOcrResult> _identityDocumentOcrResultRepository;
        private readonly INotificationService _notificationService;
        private readonly IIdentityDocumentUploadService _identityDocumentUploadService;
        private readonly IFptIdRecognitionService _fptIdRecognitionService;
        private readonly IFptPassportRecognitionService? _fptPassportRecognitionService;
        private readonly FptIdRecognitionOptions _fptIdRecognitionOptions;

        private const string IdentityDocumentReferenceType = "identity_document";
        private const string IdentityVerifiedNotificationType = "identity_verified";
        private const string IdentityRejectedNotificationType = "identity_rejected";

        public IdentityVerificationService(
            IRepository<User> userRepository,
            IRepository<Tenant> tenantRepository,
            IRepository<Landlord> landlordRepository,
            IRepository<UserIdentityDocument> userIdentityDocumentRepository,
            IRepository<IdentityDocumentOcrResult> identityDocumentOcrResultRepository,
            INotificationService notificationService,
            IIdentityDocumentUploadService identityDocumentUploadService,
            IFptIdRecognitionService fptIdRecognitionService,
            IOptions<FptIdRecognitionOptions> fptIdRecognitionOptions,
            IFptPassportRecognitionService? fptPassportRecognitionService = null)
        {
            _userRepository = userRepository;
            _tenantRepository = tenantRepository;
            _landlordRepository = landlordRepository;
            _userIdentityDocumentRepository = userIdentityDocumentRepository;
            _identityDocumentOcrResultRepository = identityDocumentOcrResultRepository;
            _notificationService = notificationService;
            _identityDocumentUploadService = identityDocumentUploadService;
            _fptIdRecognitionService = fptIdRecognitionService;
            _fptPassportRecognitionService = fptPassportRecognitionService;
            _fptIdRecognitionOptions = fptIdRecognitionOptions.Value;
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

            return (await MapDocumentDtosAsync(documents), totalCount);
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

            return (await MapDocumentDtosAsync(documents), totalCount);
        }

        private async Task<IEnumerable<IdentityDocumentDto>> MapDocumentDtosAsync(IEnumerable<UserIdentityDocument> documents)
        {
            var documentList = documents.ToList();
            var documentIds = documentList.Select(d => d.DocumentId).ToList();

            Dictionary<Guid, IdentityDocumentOcrSummaryDto> ocrMap = new();
            if (documentIds.Count > 0)
            {
                var ocrResults = await _identityDocumentOcrResultRepository.FindAsync(x => documentIds.Contains(x.DocumentId));
                ocrMap = ocrResults
                    .GroupBy(x => x.DocumentId)
                    .ToDictionary(
                        g => g.Key,
                        g => BuildOcrSummaryDto(g.OrderByDescending(x => x.ProcessedAt ?? DateTime.MinValue).First()));
            }

            return documentList
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
                    Notes = d.Notes,
                    OcrSummary = ocrMap.TryGetValue(d.DocumentId, out var ocrSummary) ? ocrSummary : null
                });
        }

        public async Task<Guid[]> AddIdentityDocumentAsync(Guid userId, IdentityDocumentUploadDto dto)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new ArgumentException("User not found.");
            }

            if (string.IsNullOrWhiteSpace(dto.DocumentType))
            {
                throw new ArgumentException("Document type is required.");
            }

            var normalizedDocumentType = dto.DocumentType.Trim().ToLowerInvariant();

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

            var uploads = BuildUploads(normalizedDocumentType, dto);
            if (uploads.Count == 0)
            {
                throw new ArgumentException("At least one identity document file is required.");
            }

            var shouldAutoApprove = false;
            string? ocrSummary = null;
            if (string.Equals(normalizedDocumentType, "national_id_card", StringComparison.OrdinalIgnoreCase))
            {
                var frontUpload = uploads.FirstOrDefault(x => string.Equals(x.Side, "front", StringComparison.OrdinalIgnoreCase));
                var backUpload = uploads.FirstOrDefault(x => string.Equals(x.Side, "back", StringComparison.OrdinalIgnoreCase));

                if (frontUpload == null || backUpload == null)
                {
                    throw new ArgumentException("National ID verification requires both front and back images.");
                }

                frontUpload.Ocr = await _fptIdRecognitionService.RecognizeAsync(frontUpload.File);
                backUpload.Ocr = await _fptIdRecognitionService.RecognizeAsync(backUpload.File);

                var strictFailure = ValidateStrictNationalId(user, frontUpload.Ocr, backUpload.Ocr, out ocrSummary);
                if (!string.IsNullOrWhiteSpace(strictFailure))
                {
                    throw new ArgumentException(strictFailure);
                }

                shouldAutoApprove = true;
            }
            else if (string.Equals(normalizedDocumentType, "passport", StringComparison.OrdinalIgnoreCase))
            {
                if (_fptPassportRecognitionService == null)
                {
                    throw new InvalidOperationException("Passport recognition service is not configured.");
                }

                if (uploads.Count != 1)
                {
                    throw new ArgumentException("Passport verification requires a single image.");
                }

                var passportUpload = uploads[0];
                passportUpload.Ocr = await _fptPassportRecognitionService.RecognizeAsync(passportUpload.File);

                var strictFailure = ValidateStrictPassport(user, tenant, passportUpload.Ocr, out ocrSummary);
                if (!string.IsNullOrWhiteSpace(strictFailure))
                {
                    throw new ArgumentException(strictFailure);
                }

                shouldAutoApprove = true;
            }

            var createdDocumentIds = new List<Guid>();
            var now = Common.Utils.VietnamTime.Now;
            var ocrResultsToPersist = new List<IdentityDocumentOcrResult>();

            foreach (var upload in uploads)
            {
                var file = upload.File;
                if (file == null || file.Length == 0)
                {
                    throw new ArgumentException("Each identity document file must be non-empty.");
                }

                var fileUrl = await _identityDocumentUploadService.UploadIdentityDocumentAsync(file);

                var document = new UserIdentityDocument
                {
                    DocumentId = Guid.NewGuid(),
                    UserId = userId,
                    DocumentType = normalizedDocumentType,
                    Side = upload.Side,
                    FileUrl = fileUrl,
                    FileKey = null,
                    MimeType = file.ContentType,
                    FileSize = file.Length,
                    Notes = CombineNotes(dto.Notes, ocrSummary, upload.Ocr),
                    UploadedAt = now,
                    VerificationStatus = shouldAutoApprove ? "verified" : "pending",
                    VerifiedAt = shouldAutoApprove ? now : null,
                    RejectionReason = null
                };

                await _userIdentityDocumentRepository.AddAsync(document);
                createdDocumentIds.Add(document.DocumentId);

                if (upload.Ocr != null)
                {
                    var ocrIdNumber = string.Equals(normalizedDocumentType, "passport", StringComparison.OrdinalIgnoreCase)
                        ? (upload.Ocr.PassportNumber ?? upload.Ocr.IdNumber)
                        : upload.Ocr.IdNumber;

                    if (!string.IsNullOrWhiteSpace(ocrIdNumber))
                    {
                        if (string.Equals(normalizedDocumentType, "passport", StringComparison.OrdinalIgnoreCase))
                        {
                            var normalizedPassport = NormalizePassportNumber(ocrIdNumber);
                            if (!string.IsNullOrWhiteSpace(normalizedPassport))
                            {
                                var tenantsWithPassport = await _tenantRepository.FindAsync(t => !string.IsNullOrWhiteSpace(t.PassportId));
                                if (tenantsWithPassport.Any(t => string.Equals(NormalizePassportNumber(t.PassportId), normalizedPassport, StringComparison.Ordinal) && t.TenantId != userId))
                                {
                                    throw new ArgumentException("Passport number is already in use by another account.");
                                }

                                var existingOcrs = await _identityDocumentOcrResultRepository.FindAsync(r => !string.IsNullOrWhiteSpace(r.IdNumber) && r.Document.UserId != userId);
                                if (existingOcrs.Any(r => string.Equals(NormalizePassportNumber(r.IdNumber), normalizedPassport, StringComparison.Ordinal)))
                                {
                                    throw new ArgumentException("Passport number is already in use by another account.");
                                }
                            }
                        }
                        else
                        {
                            var normalizedId = NormalizeIdNumber(ocrIdNumber);
                            if (!string.IsNullOrWhiteSpace(normalizedId))
                            {
                                var usersWithId = await _userRepository.FindAsync(u => !string.IsNullOrWhiteSpace(u.NationalIdCardNumber));
                                if (usersWithId.Any(u => string.Equals(NormalizeIdNumber(u.NationalIdCardNumber), normalizedId, StringComparison.Ordinal) && u.UserId != userId))
                                {
                                    throw new ArgumentException("National ID number is already in use by another account.");
                                }

                                var existingOcrs = await _identityDocumentOcrResultRepository.FindAsync(r => !string.IsNullOrWhiteSpace(r.IdNumber) && r.Document.UserId != userId);
                                if (existingOcrs.Any(r => string.Equals(NormalizeIdNumber(r.IdNumber), normalizedId, StringComparison.Ordinal)))
                                {
                                    throw new ArgumentException("National ID number is already in use by another account.");
                                }
                            }
                        }
                    }

                    var ocrResult = new IdentityDocumentOcrResult
                    {
                        OcrResultId = Guid.NewGuid(),
                        DocumentId = document.DocumentId,
                        Provider = string.Equals(normalizedDocumentType, "passport", StringComparison.OrdinalIgnoreCase)
                            ? "fpt_passport_recognition"
                            : "fpt_id_recognition",
                        ProviderErrorCode = upload.Ocr.ErrorCode,
                        ProviderErrorMessage = string.IsNullOrWhiteSpace(upload.Ocr.ErrorMessage) ? null : upload.Ocr.ErrorMessage,
                        CardType = upload.Ocr.CardType,
                        CardTypeDetail = upload.Ocr.CardTypeDetail,
                        IdNumber = ocrIdNumber,
                        FullName = upload.Ocr.FullName,
                        DateOfBirthRaw = upload.Ocr.DateOfBirth,
                        IssueDateRaw = upload.Ocr.IssueDate,
                        OverallConfidence = Convert.ToDecimal(upload.Ocr.OverallConfidence),
                        ExtractedFieldsJson = upload.Ocr.ExtractedFields.Count > 0 ? JsonSerializer.Serialize(upload.Ocr.ExtractedFields) : null,
                        FieldConfidencesJson = upload.Ocr.FieldConfidences.Count > 0 ? JsonSerializer.Serialize(upload.Ocr.FieldConfidences) : null,
                        AutoApproved = shouldAutoApprove,
                        MatchPassed = shouldAutoApprove,
                        MatchFailureReason = shouldAutoApprove ? null : "OCR strict matching did not pass.",
                        ProcessedAt = now
                    };

                    ocrResultsToPersist.Add(ocrResult);
                }
            }

            await _userIdentityDocumentRepository.SaveChangesAsync();

            if (ocrResultsToPersist.Count > 0)
            {
                foreach (var ocrResult in ocrResultsToPersist)
                {
                    await _identityDocumentOcrResultRepository.AddAsync(ocrResult);
                }

                await _identityDocumentOcrResultRepository.SaveChangesAsync();
            }

            if (shouldAutoApprove)
            {
                user.IdentityVerified = true;
                if (tenant != null && string.Equals(normalizedDocumentType, "passport", StringComparison.OrdinalIgnoreCase))
                {
                    tenant.PassportId = uploads[0].Ocr?.PassportNumber
                        ?? uploads[0].Ocr?.IdNumber
                        ?? tenant.PassportId;
                }
                _userRepository.Update(user);
                await _userRepository.SaveChangesAsync();

                if (tenant != null)
                {
                    tenant.IdentityVerificationStatus = "verified";
                    tenant.LastVerifiedAt = now;
                    _tenantRepository.Update(tenant);
                    await _tenantRepository.SaveChangesAsync();
                }
                else if (landlord != null)
                {
                    landlord.IdentityVerificationStatus = "verified";
                    landlord.LastVerifiedAt = now;
                    _landlordRepository.Update(landlord);
                    await _landlordRepository.SaveChangesAsync();
                }

                await CreateReviewNotificationAsync(userId, createdDocumentIds.First(), approved: true, rejectionReason: null);
            }
            else if (tenant != null && !string.Equals(tenant.IdentityVerificationStatus, "verified", StringComparison.OrdinalIgnoreCase))
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
                // Prevent approving identity for users under 18 based on profile or OCR data.
                DateOnly? effectiveDob = null;
                if (user.Birthday != null)
                {
                    effectiveDob = user.Birthday.Value;
                }
                else
                {
                    var ocrResults = await _identityDocumentOcrResultRepository.FindAsync(r => r.DocumentId == document.DocumentId);
                    var latest = ocrResults.OrderByDescending(x => x.ProcessedAt ?? DateTime.MinValue).FirstOrDefault();
                    if (latest != null && !string.IsNullOrWhiteSpace(latest.DateOfBirthRaw))
                    {
                        effectiveDob = TryParseDateOnly(latest.DateOfBirthRaw);
                    }
                }

                if (effectiveDob.HasValue && IsUnderage(effectiveDob.Value))
                {
                    throw new InvalidOperationException("Users under 18 cannot be identity verified.");
                }

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
            await EnsureLandlordVerifiedAsync(landlordId, "before property inspection");
        }

        public async Task EnsureUserVerifiedForListingSubmissionAsync(Guid landlordId)
        {
            await EnsureLandlordVerifiedAsync(landlordId, "before submitting a property for review");
        }

        private async Task EnsureLandlordVerifiedAsync(Guid landlordId, string actionDescription)
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

            if (user.IdentityVerified == true)
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
                throw new InvalidOperationException($"Landlord nationality is required {actionDescription}.");
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
                    ? $"Vietnamese landlords must verify identity with a national ID card or other government-issued ID (passport, driver's license, or similar) {actionDescription}."
                    : $"Foreign landlords must verify identity with a passport or other government-issued ID {actionDescription}.";
                throw new InvalidOperationException(message);
            }

            if (user.IdentityVerified == true)
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

        private List<IdentityUploadCandidate> BuildUploads(string normalizedDocumentType, IdentityDocumentUploadDto dto)
        {
            if (string.Equals(normalizedDocumentType, "national_id_card", StringComparison.OrdinalIgnoreCase))
            {
                if (dto.FrontImage == null || dto.FrontImage.Length == 0 || dto.BackImage == null || dto.BackImage.Length == 0)
                {
                    throw new ArgumentException("National ID verification requires both frontImage and backImage.");
                }

                return new List<IdentityUploadCandidate>
                {
                    new(dto.FrontImage, "front"),
                    new(dto.BackImage, "back")
                };
            }

            var uploads = new List<IdentityUploadCandidate>();

            if (dto.FrontImage != null && dto.FrontImage.Length > 0)
            {
                uploads.Add(new IdentityUploadCandidate(dto.FrontImage, "front"));
            }

            if (dto.BackImage != null && dto.BackImage.Length > 0)
            {
                uploads.Add(new IdentityUploadCandidate(dto.BackImage, "back"));
            }

            if (uploads.Count == 0 && dto.Files != null && dto.Files.Count > 0)
            {
                var side = string.IsNullOrWhiteSpace(dto.Side) ? "front" : dto.Side.Trim().ToLowerInvariant();
                uploads.AddRange(dto.Files.Where(f => f != null).Select(f => new IdentityUploadCandidate(f, side)));
            }

            return uploads;
        }

        private string? ValidateStrictNationalId(User user, FptIdRecognitionResult front, FptIdRecognitionResult back, out string ocrSummary)
        {
            ocrSummary = string.Empty;

            if (string.IsNullOrWhiteSpace(user.FullName) || user.Birthday == null || string.IsNullOrWhiteSpace(user.NationalIdCardNumber))
            {
                return "User profile must include full name, birthday, and national ID number before verification.";
            }

            if (front == null || !front.Success)
            {
                return "Failed to detect front side ID information.";
            }

            if (back == null || !back.Success)
            {
                return "Failed to detect back side ID information.";
            }

            if (string.IsNullOrWhiteSpace(front.CardType) || front.CardType.Contains("back", StringComparison.OrdinalIgnoreCase))
            {
                return "The uploaded front image was not recognized as an ID front side.";
            }

            if (string.IsNullOrWhiteSpace(back.CardType) || !back.CardType.Contains("back", StringComparison.OrdinalIgnoreCase))
            {
                return "The uploaded back image was not recognized as an ID back side.";
            }

            var normalizedUserId = NormalizeIdNumber(user.NationalIdCardNumber);
            var normalizedOcrId = NormalizeIdNumber(front.IdNumber);
            if (string.IsNullOrWhiteSpace(normalizedOcrId) || !string.Equals(normalizedUserId, normalizedOcrId, StringComparison.Ordinal))
            {
                return "National ID number does not match your profile.";
            }

            var normalizedUserName = NormalizeText(user.FullName);
            var normalizedOcrName = NormalizeText(front.FullName);
            if (string.IsNullOrWhiteSpace(normalizedOcrName) || !string.Equals(normalizedUserName, normalizedOcrName, StringComparison.Ordinal))
            {
                return "Full name on ID does not match your profile.";
            }

            var ocrBirthday = TryParseDateOnly(front.DateOfBirth);
            if (!ocrBirthday.HasValue || ocrBirthday.Value != user.Birthday.Value)
            {
                return "Date of birth on ID does not match your profile.";
            }

            if (IsUnderage(ocrBirthday.Value))
            {
                return "User must be at least 18 years old to verify identity.";
            }

            if (front.OverallConfidence < _fptIdRecognitionOptions.AutoApproveConfidenceThreshold)
            {
                return $"OCR confidence is below required threshold {_fptIdRecognitionOptions.AutoApproveConfidenceThreshold:0.00}.";
            }

            var summary = new
            {
                provider = "fpt_id_recognition",
                threshold = _fptIdRecognitionOptions.AutoApproveConfidenceThreshold,
                autoApproved = true,
                front = new
                {
                    front.CardType,
                    front.CardTypeDetail,
                    front.IdNumber,
                    front.FullName,
                    front.DateOfBirth,
                    front.OverallConfidence,
                    front.FieldConfidences
                },
                back = new
                {
                    back.CardType,
                    back.CardTypeDetail,
                    back.IssueDate,
                    back.OverallConfidence,
                    back.FieldConfidences
                },
                processedAt = Common.Utils.VietnamTime.Now
            };

            ocrSummary = JsonSerializer.Serialize(summary);
            return null;
        }

        private string? ValidateStrictPassport(User user, Tenant? tenant, FptIdRecognitionResult passport, out string ocrSummary)
        {
            ocrSummary = string.Empty;

            var normalizedPassportNumber = NormalizePassportNumber(passport.PassportNumber ?? passport.IdNumber);
            if (string.IsNullOrWhiteSpace(normalizedPassportNumber))
            {
                return "Passport number was not detected.";
            }

            if (string.IsNullOrWhiteSpace(user.FullName) && !string.IsNullOrWhiteSpace(passport.FullName))
            {
                user.FullName = passport.FullName.Trim();
            }
            else if (!string.IsNullOrWhiteSpace(user.FullName))
            {
                var normalizedUserName = NormalizeText(user.FullName);
                var normalizedPassportName = NormalizeText(passport.FullName);
                if (string.IsNullOrWhiteSpace(normalizedPassportName) || !string.Equals(normalizedUserName, normalizedPassportName, StringComparison.Ordinal))
                {
                    return "Full name on passport does not match your profile.";
                }
            }

            var passportBirthday = TryParseDateOnly(passport.DateOfBirth);
            if (user.Birthday == null && passportBirthday.HasValue)
            {
                user.Birthday = passportBirthday.Value;
            }
            else if (user.Birthday != null)
            {
                if (!passportBirthday.HasValue || passportBirthday.Value != user.Birthday.Value)
                {
                    return "Date of birth on passport does not match your profile.";
                }
            }

            if (passportBirthday.HasValue && IsUnderage(passportBirthday.Value))
            {
                return "User must be at least 18 years old to verify identity.";
            }

            if (passport.OverallConfidence < _fptIdRecognitionOptions.AutoApproveConfidenceThreshold)
            {
                return $"OCR confidence is below required threshold {_fptIdRecognitionOptions.AutoApproveConfidenceThreshold:0.00}.";
            }

            if (tenant != null)
            {
                if (string.IsNullOrWhiteSpace(tenant.PassportId))
                {
                    tenant.PassportId = normalizedPassportNumber;
                }
                else if (!string.Equals(NormalizePassportNumber(tenant.PassportId), normalizedPassportNumber, StringComparison.Ordinal))
                {
                    return "Passport number does not match your profile.";
                }
            }

            var summary = new
            {
                provider = "fpt_passport_recognition",
                threshold = _fptIdRecognitionOptions.AutoApproveConfidenceThreshold,
                autoApproved = true,
                passport = new
                {
                    passport.PassportNumber,
                    passport.FullName,
                    passport.DateOfBirth,
                    passport.PlaceOfBirth,
                    passport.Sex,
                    passport.IdNumber,
                    passport.IssueDate,
                    passport.ExpiryDate,
                    passport.OverallConfidence,
                    passport.FieldConfidences
                },
                processedAt = Common.Utils.VietnamTime.Now
            };

            ocrSummary = JsonSerializer.Serialize(summary);
            return null;
        }

        private static string? CombineNotes(string? userNotes, string? ocrSummary, FptIdRecognitionResult? ocr)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(userNotes))
            {
                parts.Add(userNotes.Trim());
            }

            if (ocr != null)
            {
                var sideSummary = JsonSerializer.Serialize(new
                {
                    ocr.CardType,
                    ocr.CardTypeDetail,
                    ocr.IdNumber,
                    ocr.FullName,
                    ocr.DateOfBirth,
                    ocr.OverallConfidence,
                    ocr.FieldConfidences
                });
                parts.Add("OCR_SIDE:" + sideSummary);
            }

            if (!string.IsNullOrWhiteSpace(ocrSummary))
            {
                parts.Add("OCR_SUMMARY:" + ocrSummary);
            }

            return parts.Count == 0 ? null : string.Join("\n", parts);
        }

        private static IdentityDocumentOcrSummaryDto BuildOcrSummaryDto(IdentityDocumentOcrResult result)
        {
            return new IdentityDocumentOcrSummaryDto
            {
                Provider = result.Provider,
                ProviderErrorCode = result.ProviderErrorCode,
                ProviderErrorMessage = result.ProviderErrorMessage,
                CardType = result.CardType,
                CardTypeDetail = result.CardTypeDetail,
                IdNumber = result.IdNumber,
                FullName = result.FullName,
                DateOfBirthRaw = result.DateOfBirthRaw,
                IssueDateRaw = result.IssueDateRaw,
                OverallConfidence = result.OverallConfidence,
                AutoApproved = result.AutoApproved,
                MatchPassed = result.MatchPassed,
                MatchFailureReason = result.MatchFailureReason,
                ProcessedAt = result.ProcessedAt
            };
        }

        private static string NormalizeIdNumber(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var digits = value.Where(char.IsDigit).ToArray();
            return new string(digits);
        }

        private static string NormalizePassportNumber(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            foreach (var ch in value.Trim().ToUpperInvariant())
            {
                if (char.IsLetterOrDigit(ch))
                {
                    builder.Append(ch);
                }
            }

            return builder.ToString();
        }

        private static string NormalizeText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder();
            foreach (var ch in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (category != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(ch);
                }
            }

            var withoutMarks = builder.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
            var compact = new StringBuilder();
            var previousWasSpace = false;
            foreach (var ch in withoutMarks)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    compact.Append(ch);
                    previousWasSpace = false;
                }
                else if (char.IsWhiteSpace(ch) && !previousWasSpace)
                {
                    compact.Append(' ');
                    previousWasSpace = true;
                }
            }

            return compact.ToString().Trim();
        }

        private static DateOnly? TryParseDateOnly(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "N/A", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var normalized = value.Trim();
            var formats = new[]
            {
                "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "yyyy-MM-dd"
            };

            foreach (var format in formats)
            {
                if (DateOnly.TryParseExact(normalized, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exactDate))
                {
                    return exactDate;
                }
            }

            return DateOnly.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                ? date
                : null;
        }

        private static bool IsUnderage(DateOnly birthDate)
        {
            var today = Common.Utils.VietnamTime.Today;
            var cutoff = today.AddYears(-18);
            return birthDate > cutoff;
        }

        private sealed class IdentityUploadCandidate
        {
            public IdentityUploadCandidate(Microsoft.AspNetCore.Http.IFormFile file, string side)
            {
                File = file;
                Side = side;
            }

            public Microsoft.AspNetCore.Http.IFormFile File { get; }

            public string Side { get; }

            public FptIdRecognitionResult? Ocr { get; set; }
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

            if (user.IdentityVerified == true)
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
