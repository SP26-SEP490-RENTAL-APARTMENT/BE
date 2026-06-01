namespace BLL.Services.Interfaces;

public interface INearbyOccupancyService
{
    Task<decimal> GetOccupancyRateAsync(
        Guid apartmentId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default);
}