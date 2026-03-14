using BLL.Services.Interfaces;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements;

public class NearbyAttractionService : BaseService<NearbyAttraction>, INearbyAttractionService
{
    public NearbyAttractionService(IRepository<NearbyAttraction> repository) : base(repository)
    {
    }
}
