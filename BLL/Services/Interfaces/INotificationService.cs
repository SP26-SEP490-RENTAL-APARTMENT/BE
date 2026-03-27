using DAL.Models;

namespace BLL.Services.Interfaces;

public interface INotificationService : IBaseService<Notification>
{
	Task<(IEnumerable<Notification> Items, int TotalCount)> GetForUserAsync(
		Guid userId,
		int page,
		int pageSize,
		bool? isRead = null,
		string? type = null);

	Task<bool> MarkAsReadAsync(Guid notificationId, Guid userId);

	Task<int> MarkAllAsReadAsync(Guid userId);
}
