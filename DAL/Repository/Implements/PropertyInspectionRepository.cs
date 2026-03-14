using DAL.Data;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace DAL.Repository.Implements
{
    public class PropertyInspectionRepository : Repository<PropertyInspection>, IPropertyInspectionRepository
    {
        public PropertyInspectionRepository(AppDbContext context) : base(context)
        {
        }
    }
}