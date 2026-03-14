using DAL.Data;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace DAL.Repository.Implements
{
    public class InspectionPhotoRepository : Repository<InspectionPhoto>, IInspectionPhotoRepository
    {
        public InspectionPhotoRepository(AppDbContext context) : base(context)
        {
        }
    }
}