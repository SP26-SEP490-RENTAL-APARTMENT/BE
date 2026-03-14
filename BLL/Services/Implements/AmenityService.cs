using BLL.Services.Interfaces;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements
{
    public class AmenityService : BaseService<Amenity>, IAmenityService
    {
        public AmenityService(IRepository<Amenity> repository) : base(repository)
        {
        }
    }
}