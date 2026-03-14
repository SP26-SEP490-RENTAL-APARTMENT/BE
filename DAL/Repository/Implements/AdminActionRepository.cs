using DAL.Data;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace DAL.Repository.Implements
{
    public class AdminActionRepository : Repository<AdminAction>, IAdminActionRepository
    {
        public AdminActionRepository(AppDbContext context) : base(context)
        {
        }
    }
}