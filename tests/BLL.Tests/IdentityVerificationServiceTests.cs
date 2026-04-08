using System;
using System.Linq;
using System.Threading.Tasks;
using BLL.Services.Implements;
using BLL.Services.Interfaces;
using Common.DTOs;
using DAL.Models;

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
            IdentityVerified = false
        });

        var tenantRepo = new InMemoryRepository<Tenant>(t => t.TenantId, new Tenant
        {
            TenantId = userId,
            Nationality = "VN",
            IdentityVerificationStatus = null
        });

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
        var sut = new IdentityVerificationService(userRepo, tenantRepo, documentRepo, notificationService, new NoOpImageService());

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
            IdentityVerified = true
        });

        var tenantRepo = new InMemoryRepository<Tenant>(t => t.TenantId, new Tenant
        {
            TenantId = userId,
            Nationality = "US",
            IdentityVerificationStatus = "verified"
        });

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
        var sut = new IdentityVerificationService(userRepo, tenantRepo, documentRepo, notificationService, new NoOpImageService());

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
}

internal sealed class NoOpImageService : IImageService
{
    public Task<string> UploadImageAsync(Microsoft.AspNetCore.Http.IFormFile file)
    {
        return Task.FromResult(string.Empty);
    }
}