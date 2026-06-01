namespace BLL.Services.Interfaces;

public interface IOccupancyAggregationService
{
    Task RefreshCacheAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default);
}