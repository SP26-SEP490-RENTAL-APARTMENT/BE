using Common.DTOs;
using DAL.Models;

namespace BLL.Services.Interfaces;

public interface IPropertyInspectionService : IBaseService<PropertyInspection>
{
	Task<PropertyInspection> StartInspectionAsync(Guid inspectionId, Guid staffId);
	Task<PropertyInspection> CompleteInspectionAsync(Guid inspectionId, Guid staffId, CompletePropertyInspectionDto dto);
	Task<PropertyInspection> CancelInspectionAsync(Guid inspectionId, Guid staffId, string reason);
	Task<PropertyInspection> ReviewInspectionAsync(Guid inspectionId, Guid adminId, string decision, string? reason);
}
