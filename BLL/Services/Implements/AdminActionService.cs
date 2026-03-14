using BLL.Services.Interfaces;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements;

public class AdminActionService : BaseService<AdminAction>, IAdminActionService
{
    public AdminActionService(IRepository<AdminAction> repository) : base(repository)
    {
    }
}
