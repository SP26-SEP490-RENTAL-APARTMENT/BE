using BLL.Services.Interfaces;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements;

public class PackageService : BaseService<Package>, IPackageService
{
    public PackageService(IRepository<Package> repository) : base(repository)
    {
    }
}
