using BLL.Services.Interfaces;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements;

public class LandlordService : BaseService<Landlord>, ILandlordService
{
    public LandlordService(IRepository<Landlord> repository) : base(repository)
    {
    }
}
