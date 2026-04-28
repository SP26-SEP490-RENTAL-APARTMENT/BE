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
        string? search = null,
        Dictionary<string, string>? filters = null);

    Task<SubscriptionPlanDto?> GetCurrentSubscriptionAsync(Guid landlordId);

    Task<LandlordPayoutProfileDto?> GetPayoutProfileAsync(Guid landlordId);

    Task<LandlordPayoutProfileDto> UpdateBankPayoutProfileAsync(Guid landlordId, UpdateBankPayoutProfileRequestDto request);

    Task<LandlordPayoutProfileDto> UpdateMomoPayoutProfileAsync(Guid landlordId, UpdateMomoPayoutProfileRequestDto request);

    Task<LandlordPayoutProfileDto> UpsertPayoutProfileAsync(Guid landlordId, UpsertLandlordPayoutProfileRequestDto request);
}
