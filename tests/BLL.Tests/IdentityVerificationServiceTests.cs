using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BLL.Services.Implements;
using BLL.Services.Interfaces;
using Common.DTOs;
using DAL.Models;
using Microsoft.AspNetCore.Http;

namespace BLL.Tests;

public class IdentityVerificationServiceTests
{
    [Fact]
    public async Task ReviewIdentityDocumentAsync_WhenApproved_CreatesVerifiedNotificationAndMarksUserVerified()
    {
        var userId = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        var userRepo = new InMemoryRepository<User>(u => u.UserId, new User
        {
            UserId = userId,
            Role = "tenant",
            Nationality = "VN",
            IdentityVerified = false
        });

        var tenantRepo = new InMemoryRepository<Tenant>(t => t.TenantId, new Tenant
        {
            TenantId = userId,
            IdentityVerificationStatus = null
        });

        var landlordRepo = new InMemoryRepository<Landlord>(l => l.LandlordId);

        var documentRepo = new InMemoryRepository<UserIdentityDocument>(d => d.DocumentId, new UserIdentityDocument
        {
            DocumentId = documentId,
            UserId = userId,
            DocumentType = "national_id_card",
            FileUrl = "https://example.test/document.pdf",
            VerificationStatus = "pending",
            UploadedAt = DateTime.UtcNow
        });

        var notificationRepo = new InMemoryRepository<Notification>(n => n.NotificationId);
        var notificationService = new NotificationService(notificationRepo);
        var sut = new IdentityVerificationService(userRepo, tenantRepo, landlordRepo, documentRepo, notificationService, new NoOpIdentityDocumentUploadService());

        await sut.ReviewIdentityDocumentAsync(new ReviewIdentityDocumentDto
        {
            DocumentId = documentId,
            Approved = true
        });

        var updatedDocument = await documentRepo.GetByIdAsync(documentId);
        var updatedUser = await userRepo.GetByIdAsync(userId);

        Assert.NotNull(updatedDocument);
        Assert.Equal("verified", updatedDocument!.VerificationStatus);
        Assert.NotNull(updatedDocument.VerifiedAt);
        Assert.Null(updatedDocument.RejectionReason);

        Assert.NotNull(updatedUser);
        Assert.True(updatedUser!.IdentityVerified);

        var updatedTenant = await tenantRepo.GetByIdAsync(userId);
        Assert.NotNull(updatedTenant);
        Assert.Equal("verified", updatedTenant!.IdentityVerificationStatus);
        Assert.NotNull(updatedTenant.LastVerifiedAt);

        var notification = notificationRepo.Items.Single();
        Assert.Equal(userId, notification.UserId);
        Assert.Equal("identity_verified", notification.Type);
        Assert.Equal("Identity verification approved", notification.Title);
        Assert.Equal("Your identity document has been approved.", notification.Message);
        Assert.Equal(documentId, notification.ReferenceId);
        Assert.Equal("identity_document", notification.ReferenceType);
        Assert.False(notification.IsRead);
        Assert.NotNull(notification.CreatedAt);
    }

    [Fact]
    public async Task ReviewIdentityDocumentAsync_WhenRejected_CreatesRejectedNotificationAndClearsDocumentState()
    {
        var userId = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        var userRepo = new InMemoryRepository<User>(u => u.UserId, new User
        {
            UserId = userId,
            Role = "tenant",
            Nationality = "US",
            IdentityVerified = true
        });

        var tenantRepo = new InMemoryRepository<Tenant>(t => t.TenantId, new Tenant
        {
            TenantId = userId,
            IdentityVerificationStatus = "verified"
        });

        var landlordRepo = new InMemoryRepository<Landlord>(l => l.LandlordId);

        var documentRepo = new InMemoryRepository<UserIdentityDocument>(d => d.DocumentId, new UserIdentityDocument
        {
            DocumentId = documentId,
            UserId = userId,
            DocumentType = "passport",
            FileUrl = "https://example.test/document.pdf",
            VerificationStatus = "verified",
            VerifiedAt = DateTime.UtcNow,
            UploadedAt = DateTime.UtcNow
        });

        var notificationRepo = new InMemoryRepository<Notification>(n => n.NotificationId);
        var notificationService = new NotificationService(notificationRepo);
        var sut = new IdentityVerificationService(userRepo, tenantRepo, landlordRepo, documentRepo, notificationService, new NoOpIdentityDocumentUploadService());

        await sut.ReviewIdentityDocumentAsync(new ReviewIdentityDocumentDto
        {
            DocumentId = documentId,
            Approved = false,
            RejectionReason = "Document image is blurry."
        });

        var updatedDocument = await documentRepo.GetByIdAsync(documentId);
        var updatedUser = await userRepo.GetByIdAsync(userId);

        Assert.NotNull(updatedDocument);
        Assert.Equal("rejected", updatedDocument!.VerificationStatus);
        Assert.Null(updatedDocument.VerifiedAt);
        Assert.Equal("Document image is blurry.", updatedDocument.RejectionReason);

        Assert.NotNull(updatedUser);
        Assert.False(updatedUser!.IdentityVerified);

        var updatedTenant = await tenantRepo.GetByIdAsync(userId);
        Assert.NotNull(updatedTenant);
        Assert.Equal("rejected", updatedTenant!.IdentityVerificationStatus);
        Assert.Null(updatedTenant.LastVerifiedAt);

        var notification = notificationRepo.Items.Single();
        Assert.Equal(userId, notification.UserId);
        Assert.Equal("identity_rejected", notification.Type);
        Assert.Equal("Identity verification rejected", notification.Title);
        Assert.Equal("Your identity document was rejected: Document image is blurry.", notification.Message);
        Assert.Equal(documentId, notification.ReferenceId);
        Assert.Equal("identity_document", notification.ReferenceType);
        Assert.False(notification.IsRead);
        Assert.NotNull(notification.CreatedAt);
    }

    [Fact]
    public async Task AddIdentityDocumentAsync_WhenTenantUploads_SetsTenantStatusPending()
    {
        var userId = Guid.NewGuid();

        var userRepo = new InMemoryRepository<User>(u => u.UserId, new User
        {
            UserId = userId,
            Role = "tenant",
            Nationality = "VN",
            IdentityVerified = false
        });

        var tenantRepo = new InMemoryRepository<Tenant>(t => t.TenantId, new Tenant
        {
            TenantId = userId,
            IdentityVerificationStatus = "not_started"
        });

        var landlordRepo = new InMemoryRepository<Landlord>(l => l.LandlordId);

        var documentRepo = new InMemoryRepository<UserIdentityDocument>(d => d.DocumentId);
        var notificationRepo = new InMemoryRepository<Notification>(n => n.NotificationId);
        var notificationService = new NotificationService(notificationRepo);

        var sut = new IdentityVerificationService(userRepo, tenantRepo, landlordRepo, documentRepo, notificationService, new NoOpIdentityDocumentUploadService());

        IFormFile file = new TestFormFile("id.jpg", "image/jpeg", new byte[] { 1, 2, 3 });

        var dto = new IdentityDocumentUploadDto
        {
            DocumentType = "drivers_license",
            Side = "front",
            Files = { file }
        };

        var documentIds = await sut.AddIdentityDocumentAsync(userId, dto);

        Assert.Single(documentIds);

        var updatedTenant = await tenantRepo.GetByIdAsync(userId);
        Assert.NotNull(updatedTenant);
        Assert.Equal("pending", updatedTenant!.IdentityVerificationStatus);
    }

    [Fact]
    public async Task ReviewIdentityDocumentAsync_WhenRejectedWithoutReason_ThrowsArgumentException()
    {
        var userId = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        var userRepo = new InMemoryRepository<User>(u => u.UserId, new User
        {
            UserId = userId,
            Role = "tenant",
            Nationality = "VN",
            IdentityVerified = false
        });

        var tenantRepo = new InMemoryRepository<Tenant>(t => t.TenantId, new Tenant
        {
            TenantId = userId,
            IdentityVerificationStatus = "pending"
        });

        var landlordRepo = new InMemoryRepository<Landlord>(l => l.LandlordId);

        var documentRepo = new InMemoryRepository<UserIdentityDocument>(d => d.DocumentId, new UserIdentityDocument
        {
            DocumentId = documentId,
            UserId = userId,
            DocumentType = "national_id_card",
            FileUrl = "https://example.test/document.pdf",
            VerificationStatus = "pending",
            UploadedAt = DateTime.UtcNow
        });

        var notificationRepo = new InMemoryRepository<Notification>(n => n.NotificationId);
        var notificationService = new NotificationService(notificationRepo);
        var sut = new IdentityVerificationService(userRepo, tenantRepo, landlordRepo, documentRepo, notificationService, new NoOpIdentityDocumentUploadService());

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => sut.ReviewIdentityDocumentAsync(new ReviewIdentityDocumentDto
        {
            DocumentId = documentId,
            Approved = false,
            RejectionReason = "   "
        }));

        Assert.Equal("Rejection reason is required when rejecting an identity document.", ex.Message);
    }

    [Fact]
    public async Task EnsureUserVerifiedForInspectionAsync_WhenLandlordVerified_Succeeds()
    {
        var landlordId = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        var userRepo = new InMemoryRepository<User>(u => u.UserId, new User
        {
            UserId = landlordId,
            Role = "landlord",
            Nationality = "VN",
            IdentityVerified = false
        });

        var tenantRepo = new InMemoryRepository<Tenant>(t => t.TenantId);

        var landlordRepo = new InMemoryRepository<Landlord>(l => l.LandlordId, new Landlord
        {
            LandlordId = landlordId,
            IdentityVerificationStatus = "not_started"
        });

        var documentRepo = new InMemoryRepository<UserIdentityDocument>(d => d.DocumentId, new UserIdentityDocument
        {
            DocumentId = documentId,
            UserId = landlordId,
            DocumentType = "national_id_card",
            FileUrl = "https://example.test/document.pdf",
            VerificationStatus = "verified",
            VerifiedAt = DateTime.UtcNow,
            UploadedAt = DateTime.UtcNow
        });

        var notificationRepo = new InMemoryRepository<Notification>(n => n.NotificationId);
        var notificationService = new NotificationService(notificationRepo);
        var sut = new IdentityVerificationService(userRepo, tenantRepo, landlordRepo, documentRepo, notificationService, new NoOpIdentityDocumentUploadService());

        await sut.EnsureUserVerifiedForInspectionAsync(landlordId);

        var updatedUser = await userRepo.GetByIdAsync(landlordId);
        var updatedLandlord = await landlordRepo.GetByIdAsync(landlordId);

        Assert.NotNull(updatedUser);
        Assert.True(updatedUser!.IdentityVerified);

        Assert.NotNull(updatedLandlord);
        Assert.Equal("verified", updatedLandlord!.IdentityVerificationStatus);
        Assert.NotNull(updatedLandlord.LastVerifiedAt);
    }

    [Fact]
    public async Task EnsureUserVerifiedForInspectionAsync_WhenLandlordNotVerified_ThrowsInvalidOperationException()
    {
        var landlordId = Guid.NewGuid();

        var userRepo = new InMemoryRepository<User>(u => u.UserId, new User
        {
            UserId = landlordId,
            Role = "landlord",
            Nationality = "VN",
            IdentityVerified = false
        });

        var tenantRepo = new InMemoryRepository<Tenant>(t => t.TenantId);

        var landlordRepo = new InMemoryRepository<Landlord>(l => l.LandlordId, new Landlord
        {
            LandlordId = landlordId,
            IdentityVerificationStatus = "not_started"
        });

        var documentRepo = new InMemoryRepository<UserIdentityDocument>(d => d.DocumentId);

        var notificationRepo = new InMemoryRepository<Notification>(n => n.NotificationId);
        var notificationService = new NotificationService(notificationRepo);
        var sut = new IdentityVerificationService(userRepo, tenantRepo, landlordRepo, documentRepo, notificationService, new NoOpIdentityDocumentUploadService());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.EnsureUserVerifiedForInspectionAsync(landlordId));

        Assert.Equal("Vietnamese landlords must verify identity with a national ID card or other government-issued ID (passport, driver's license, or similar) before property inspection.", ex.Message);
    }
}

internal sealed class NoOpIdentityDocumentUploadService : IIdentityDocumentUploadService
{
    public Task<string> UploadIdentityDocumentAsync(IFormFile file)
    {
        return Task.FromResult(string.Empty);
    }
}

internal sealed class TestFormFile : IFormFile
{
    private readonly byte[] _content;

    public TestFormFile(string fileName, string contentType, byte[] content)
    {
        FileName = fileName;
        ContentType = contentType;
        _content = content;
    }

    public string ContentType { get; }

    public string ContentDisposition => string.Empty;

    public IHeaderDictionary Headers => throw new NotImplementedException();

    public long Length => _content.LongLength;

    public string Name => "file";

    public string FileName { get; }

    public void CopyTo(Stream target)
    {
        target.Write(_content, 0, _content.Length);
    }

    public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
    {
        return target.WriteAsync(_content, cancellationToken).AsTask();
    }

    public Stream OpenReadStream()
    {
        return new MemoryStream(_content, writable: false);
    }
}