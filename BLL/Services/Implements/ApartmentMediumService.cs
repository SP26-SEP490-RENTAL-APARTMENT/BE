using BLL.Services.Interfaces;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements;

public class ApartmentMediumService : BaseService<ApartmentMedium>, IApartmentMediumService
{
    public ApartmentMediumService(IRepository<ApartmentMedium> repository) : base(repository)
    {
    }
}
