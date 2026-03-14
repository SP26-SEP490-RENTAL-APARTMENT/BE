using BLL.Services.Interfaces;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements;

public sealed class PackageItemService(IRepository<PackageItem> repository)
    : BaseService<PackageItem>(repository), IPackageItemService
{
}
