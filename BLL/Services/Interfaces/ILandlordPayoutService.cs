using Common.DTOs;
using DAL.Models;

namespace BLL.Services.Interfaces;

public interface ILandlordPayoutService
{
    Task<LandlordPayoutResponseDto> CreatePayoutAsync(Guid landlordId, CreateLandlordPayoutRequestDto request, CancellationToken cancellationToken = default);

    Task<(IEnumerable<LandlordPayoutResponseDto> Items, int TotalCount)> GetPayoutHistoryAsync(
        Guid landlordId,
        int page,
        int pageSize,
        string? sortBy = null,
        string? sortOrder = null,
        string? search = null,
        Dictionary<string, string>? filters = null);

    Task<LandlordPayoutResponseDto?> GetPayoutByIdAsync(Guid landlordId, Guid payoutId);

    Task<int> SyncProcessingPayoutsAsync(CancellationToken cancellationToken = default);
}
