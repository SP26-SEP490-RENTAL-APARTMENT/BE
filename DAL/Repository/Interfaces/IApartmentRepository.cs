using System;
using System.Threading.Tasks;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace DAL.Repository.Interfaces
{
    public interface IApartmentRepository : IRepository<Apartment>
    {
        Task<Apartment?> GetApartmentWithDetailsAsync(Guid id);
    }
}