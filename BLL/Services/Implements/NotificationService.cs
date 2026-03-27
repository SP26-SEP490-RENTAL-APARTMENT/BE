using BLL.Services.Interfaces;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements;

public class NotificationService : BaseService<Notification>, INotificationService
{
    public NotificationService(IRepository<Notification> repository) : base(repository)
    {
    }

    public async Task<(IEnumerable<Notification> Items, int TotalCount)> GetForUserAsync(
        Guid userId,
        int page,
        int pageSize,
        bool? isRead = null,
        string? type = null)
    {
        var filters = new Dictionary<string, string>
        {
            ["UserId"] = userId.ToString()
        };

        if (isRead.HasValue)
        {
            filters["IsRead"] = isRead.Value ? "true" : "false";
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            filters["Type"] = type;
        }

        var allowedColumns = new[]
        {
            "NotificationId",
            "UserId",
            "Type",
            "Title",
            "Message",
            "ReferenceId",
            "ReferenceType",
            "IsRead",
            "ReadAt",
            "CreatedAt"
        };

        return await GetAllAsync(
            page,
            pageSize,
            sortBy: "CreatedAt",
            sortOrder: "desc",
            search: null,
            filters: filters,
            allowedColumns: allowedColumns);
    }

    public async Task<bool> MarkAsReadAsync(Guid notificationId, Guid userId)
    {
        var notification = await _repository.GetByIdAsync(notificationId);
        if (notification == null || notification.UserId != userId)
        {
            return false;
        }

        if (notification.IsRead != true)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            _repository.Update(notification);
            await _repository.SaveChangesAsync();
        }

        return true;
    }

    public async Task<int> MarkAllAsReadAsync(Guid userId)
    {
        var unread = await _repository.FindAsync(
            n => n.UserId == userId && (n.IsRead == null || n.IsRead == false));

        var updatedCount = 0;
        foreach (var notification in unread)
        {
            if (notification.IsRead != true)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
                _repository.Update(notification);
                updatedCount++;
            }
        }

        if (updatedCount > 0)
        {
            await _repository.SaveChangesAsync();
        }

        return updatedCount;
    }
}
