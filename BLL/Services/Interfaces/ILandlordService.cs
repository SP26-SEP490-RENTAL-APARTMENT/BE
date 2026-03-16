using DAL.Models;
using Common.DTOs;

namespace BLL.Services.Interfaces;

public interface ILandlordService : IBaseService<Landlord>
{
    Task<Landlord?> GetByUserIdAsync(Guid userId);

    Task<(IEnumerable<Apartment> Items, int TotalCount)> GetOwnApartmentsAsync(
        Guid landlordId,
        int page,
        int pageSize,
        string? sortBy = null,
        string? sortOrder = null,
        string? search = null);

    Task<SubscriptionPlanDto?> GetCurrentSubscriptionAsync(Guid landlordId);
}
