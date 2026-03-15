using System;
using System.Threading.Tasks;
using DAL.Data;
using DAL.Models;
using DAL.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repository.Implements
{
    public class ApartmentRepository : Repository<Apartment>, IApartmentRepository
    {
        public ApartmentRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<Apartment?> GetApartmentWithDetailsAsync(Guid id)
        {
            return await _dbSet
                .Include(a => a.Room)
                .Include(a => a.Amenities)
                .Include(a => a.ApartmentMedia)
                .FirstOrDefaultAsync(a => a.ApartmentId == id);
        }
    }
}