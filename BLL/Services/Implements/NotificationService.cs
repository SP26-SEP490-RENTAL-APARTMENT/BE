using BLL.Services.Interfaces;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements;

public class NotificationService : BaseService<Notification>, INotificationService
{
    public NotificationService(IRepository<Notification> repository) : base(repository)
    {
    }
}
