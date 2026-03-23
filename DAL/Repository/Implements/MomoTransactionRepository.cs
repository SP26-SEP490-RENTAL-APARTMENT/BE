using DAL.Data;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace DAL.Repository.Implements
{
    public class MomoTransactionRepository : Repository<MomoTransaction>, IMomoTransactionRepository
    {
        public MomoTransactionRepository(AppDbContext context) : base(context)
        {
        }
    }
}
