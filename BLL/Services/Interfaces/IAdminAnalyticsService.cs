using System.Threading;
using System.Threading.Tasks;
using Common.DTOs;

namespace BLL.Services.Interfaces;

public interface IAdminAnalyticsService
{
    Task<AdminAnalyticsSnapshotDto> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
