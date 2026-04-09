using BLL.Services.Interfaces;
using Common.DTOs;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements;

public sealed class PropertyInspectionService(
    IRepository<PropertyInspection> repository,
    IRepository<InspectionPhoto> inspectionPhotoRepository,
    IImageService imageService)
    : BaseService<PropertyInspection>(repository), IPropertyInspectionService
{
    private readonly IRepository<InspectionPhoto> _inspectionPhotoRepository = inspectionPhotoRepository;
    private readonly IImageService _imageService = imageService;

    public override async Task<(IEnumerable<PropertyInspection> Items, int TotalCount)> GetAllAsync(
        int page,
        int pageSize,
        string? sortBy = null,
        string? sortOrder = null,
        string? search = null,
        Dictionary<string, string>? filters = null,
        IEnumerable<string>? allowedColumns = null)
    {
        var effectiveAllowedColumns = new[]
        {
            "InspectionId",
            "ApartmentId",
            "InspectorId",
            "ScheduledDate",
            "CompletedDate",
            "Status",
            "OverallCondition",
            "ApprovedForListing",
            "ApprovedAt",
            "ApprovedBy"
        };

        var (items, totalCount) = await base.GetAllAsync(page, pageSize, sortBy, sortOrder, search, filters, effectiveAllowedColumns);
        var inspectionList = items.ToList();

        if (!inspectionList.Any())
        {
            return (inspectionList, totalCount);
        }

        var inspectionIds = inspectionList.Select(i => i.InspectionId).ToList();
        var allPhotos = await _inspectionPhotoRepository.FindAsync(p => inspectionIds.Contains(p.InspectionId));
        var photoLookup = allPhotos.ToLookup(p => p.InspectionId);

        foreach (var inspection in inspectionList)
        {
            inspection.InspectionPhotos = photoLookup[inspection.InspectionId].ToList();
        }

        return (inspectionList, totalCount);
    }

    public override async Task<PropertyInspection?> GetByIdAsync(Guid id)
    {
        var inspection = await base.GetByIdAsync(id);
        if (inspection == null)
        {
            return null;
        }

        var photos = await _inspectionPhotoRepository.FindAsync(p => p.InspectionId == id);
        inspection.InspectionPhotos = photos.ToList();
        return inspection;
    }

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
        inspection.CompletedDate = DateOnly.FromDateTime(Common.Utils.VietnamTime.Now);
        inspection.Status = "pending";

        _repository.Update(inspection);

        for (var index = 0; index < dto.Photos.Count; index++)
        {
            var photo = dto.Photos[index];
            if (photo == null || photo.Length == 0)
            {
                throw new ArgumentException("Each inspection photo must be a non-empty file.");
            }

            var fileUrl = await _imageService.UploadImageAsync(photo);
            var description = dto.PhotoDescriptions != null && index < dto.PhotoDescriptions.Count
                ? dto.PhotoDescriptions[index]
                : null;

            bool? isIssue = null;
            if (dto.PhotoIsIssues != null && index < dto.PhotoIsIssues.Count)
            {
                isIssue = dto.PhotoIsIssues[index];
            }

            var inspectionPhoto = new InspectionPhoto
            {
                PhotoId = Guid.NewGuid(),
                InspectionId = inspection.InspectionId,
                FileUrl = fileUrl,
                FileKey = null,
                Description = description,
                IsIssue = isIssue,
                UploadedAt = Common.Utils.VietnamTime.Now
            };

            // Keep the in-memory aggregate in sync so response mapping includes uploaded photos.
            inspection.InspectionPhotos.Add(inspectionPhoto);
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
        inspection.ApprovedAt = Common.Utils.VietnamTime.Now;
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
