using BLL.Services.Interfaces;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements;

public class ApartmentService : BaseService<Apartment>, IApartmentService
{
    public ApartmentService(IRepository<Apartment> repository) : base(repository)
    {
    }
}
