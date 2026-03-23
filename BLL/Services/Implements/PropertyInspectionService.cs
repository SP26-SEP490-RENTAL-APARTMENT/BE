using BLL.Services.Interfaces;
using Common.DTOs;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements;

public sealed class PropertyInspectionService(
    IRepository<PropertyInspection> repository,
    IRepository<InspectionPhoto> inspectionPhotoRepository)
    : BaseService<PropertyInspection>(repository), IPropertyInspectionService
{
    private readonly IRepository<InspectionPhoto> _inspectionPhotoRepository = inspectionPhotoRepository;

    public async Task<PropertyInspection> StartInspectionAsync(Guid inspectionId, Guid staffId)
    {
        var inspection = await _repository.GetByIdAsync(inspectionId)
            ?? throw new KeyNotFoundException("Inspection not found.");

        if (inspection.InspectorId != staffId)
        {
            throw new InvalidOperationException("Only assigned staff can start this inspection.");
        }

        if (!string.Equals(inspection.Status, "scheduled", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only scheduled inspections can be started.");
        }

        inspection.Status = "in_progress";
        _repository.Update(inspection);
        await _repository.SaveChangesAsync();
        return inspection;
    }

    public async Task<PropertyInspection> CompleteInspectionAsync(Guid inspectionId, Guid staffId, CompletePropertyInspectionDto dto)
    {
        var inspection = await _repository.GetByIdAsync(inspectionId)
            ?? throw new KeyNotFoundException("Inspection not found.");

        if (inspection.InspectorId != staffId)
        {
            throw new InvalidOperationException("Only assigned staff can complete this inspection.");
        }

        if (!string.Equals(inspection.Status, "in_progress", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only in-progress inspections can be completed.");
        }

        inspection.OverallCondition = dto.OverallCondition;
        inspection.IssuesFound = dto.IssuesFound;
        inspection.Recommendations = dto.Recommendations;
        inspection.CompletedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        inspection.Status = "pending";

        _repository.Update(inspection);

        foreach (var photo in dto.Photos)
        {
            var inspectionPhoto = new InspectionPhoto
            {
                PhotoId = Guid.NewGuid(),
                InspectionId = inspection.InspectionId,
                FileUrl = photo.FileUrl,
                FileKey = photo.FileKey,
                Description = photo.Description,
                IsIssue = photo.IsIssue,
                UploadedAt = DateTime.UtcNow
            };
            await _inspectionPhotoRepository.AddAsync(inspectionPhoto);
        }

        await _repository.SaveChangesAsync();
        return inspection;
    }

    public async Task<PropertyInspection> CancelInspectionAsync(Guid inspectionId, Guid staffId, string reason)
    {
        var inspection = await _repository.GetByIdAsync(inspectionId)
            ?? throw new KeyNotFoundException("Inspection not found.");

        if (inspection.InspectorId != staffId)
        {
            throw new InvalidOperationException("Only assigned staff can cancel this inspection.");
        }

        if (!string.Equals(inspection.Status, "scheduled", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(inspection.Status, "in_progress", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only scheduled or in-progress inspections can be cancelled.");
        }

        inspection.Status = "failed";
        inspection.Recommendations = string.IsNullOrWhiteSpace(inspection.Recommendations)
            ? $"Cancellation reason: {reason}"
            : $"{inspection.Recommendations}\nCancellation reason: {reason}";

        _repository.Update(inspection);
        await _repository.SaveChangesAsync();
        return inspection;
    }

    public async Task<PropertyInspection> ReviewInspectionAsync(Guid inspectionId, Guid adminId, string decision, string? reason)
    {
        var inspection = await _repository.GetByIdAsync(inspectionId)
            ?? throw new KeyNotFoundException("Inspection not found.");

        if (!string.Equals(inspection.Status, "pending", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only pending inspections can be reviewed.");
        }

        var isApprove = string.Equals(decision, "approve", StringComparison.OrdinalIgnoreCase);

        inspection.ApprovedBy = adminId;
        inspection.ApprovedAt = DateTime.UtcNow;
        inspection.ApprovedForListing = isApprove;
        inspection.Status = isApprove ? "passed" : "re_inspection_needed";

        if (!isApprove && !string.IsNullOrWhiteSpace(reason))
        {
            inspection.Recommendations = string.IsNullOrWhiteSpace(inspection.Recommendations)
                ? $"Review note: {reason}"
                : $"{inspection.Recommendations}\nReview note: {reason}";
        }

        _repository.Update(inspection);
        await _repository.SaveChangesAsync();
        return inspection;
    }
}
