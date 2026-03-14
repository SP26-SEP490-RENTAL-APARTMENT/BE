using DAL.Data;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace DAL.Repository.Implements
{
    public class PackageItemRepository : Repository<PackageItem>, IPackageItemRepository
    {
        public PackageItemRepository(AppDbContext context) : base(context)
        {
        }
    }
}