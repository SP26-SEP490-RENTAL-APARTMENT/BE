using Common.DTOs;
using DAL.Models;

namespace BLL.Services.Interfaces;

public interface IPropertyInspectionService : IBaseService<PropertyInspection>
{
	Task<(IEnumerable<PropertyInspection> Items, int TotalCount)> GetByStaffIdAsync(
		Guid staffId,
		int page,
		int pageSize,
		string? sortBy = null,
		string? sortOrder = null,
		string? search = null,
		Dictionary<string, string>? filters = null);
	Task<PropertyInspection> StartInspectionAsync(Guid inspectionId, Guid staffId);
	Task<PropertyInspection> CompleteInspectionAsync(Guid inspectionId, Guid staffId, CompletePropertyInspectionDto dto);
	Task<PropertyInspection> CancelInspectionAsync(Guid inspectionId, Guid staffId, string reason);
	Task<PropertyInspection> ReviewInspectionAsync(Guid inspectionId, Guid adminId, string decision, string? reason);
}
