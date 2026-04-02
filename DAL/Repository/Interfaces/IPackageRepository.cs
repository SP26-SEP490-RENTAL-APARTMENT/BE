using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace DAL.Repository.Interfaces
{
    public interface IPackageRepository : IRepository<Package>
    {
        Task<(IEnumerable<Package> Items, int TotalCount)> GetByApartmentIdAsync(
            Guid apartmentId,
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null,
            string? search = null,
            Dictionary<string, string>? filters = null,
            IEnumerable<string>? allowedColumns = null);

        Task<(IEnumerable<Package> Items, int TotalCount)> GetAllWithDetailsAsync(
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null,
            string? search = null,
            Dictionary<string, string>? filters = null,
            IEnumerable<string>? allowedColumns = null
        );
    }
}
