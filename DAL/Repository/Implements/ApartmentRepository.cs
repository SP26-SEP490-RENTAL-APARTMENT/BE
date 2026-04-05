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
                .Include(a => a.Landlord)
                    .ThenInclude(l => l.LandlordNavigation)
                .FirstOrDefaultAsync(a => a.ApartmentId == id);
        }

        public override async Task<(IEnumerable<Apartment> Items, int TotalCount)> GetAllAsync(
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null,
            string? search = null,
            Dictionary<string, string>? filters = null,
            IEnumerable<string>? allowedColumns = null)
        {
            var query = _dbSet.AsQueryable();

            query = ApplyFilters(query, filters, allowedColumns);
            query = ApplyApartmentSearchWithLandlordName(query, search, allowedColumns);
            query = ApplySorting(query, sortBy, sortOrder);

            var totalCount = await query.CountAsync();
            var items = await query
                .Include(a => a.Room)
                .Include(a => a.Amenities)
                .Include(a => a.ApartmentMedia)
                .Include(a => a.Landlord)
                    .ThenInclude(l => l.LandlordNavigation)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<(IEnumerable<Apartment> Items, int TotalCount)> GetAllPublicAsync(
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null,
            string? search = null,
            Dictionary<string, string>? filters = null,
            IEnumerable<string>? allowedColumns = null)
        {
            var query = _dbSet.Where(a => a.Status == null || a.Status.ToLower() == "posted");

            query = ApplyFilters(query, filters, allowedColumns);
            query = ApplyApartmentSearchWithLandlordName(query, search, allowedColumns);
            query = ApplySorting(query, sortBy, sortOrder);

            var totalCount = await query.CountAsync();
            var items = await query
                .Include(a => a.Room)
                .Include(a => a.Amenities)
                .Include(a => a.ApartmentMedia)
                .Include(a => a.Landlord)
                    .ThenInclude(l => l.LandlordNavigation)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        private IQueryable<Apartment> ApplyApartmentSearchWithLandlordName(
            IQueryable<Apartment> query,
            string? search,
            IEnumerable<string>? allowedColumns)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                return query;
            }

            var normalizedSearch = search.ToLower();
            HashSet<string>? allowed = null;
            if (allowedColumns != null)
            {
                allowed = new HashSet<string>(allowedColumns, StringComparer.OrdinalIgnoreCase);
            }

            var allowTitle = allowed == null || allowed.Contains("Title");
            var allowDescription = allowed == null || allowed.Contains("Description");
            var allowAddress = allowed == null || allowed.Contains("Address");
            var allowDistrict = allowed == null || allowed.Contains("District");
            var allowCity = allowed == null || allowed.Contains("City");
            var allowStatus = allowed == null || allowed.Contains("Status");

            return query.Where(a =>
                (allowTitle && a.Title != null && a.Title.ToLower().Contains(normalizedSearch))
                || (allowDescription && a.Description != null && a.Description.ToLower().Contains(normalizedSearch))
                || (allowAddress && a.Address != null && a.Address.ToLower().Contains(normalizedSearch))
                || (allowDistrict && a.District != null && a.District.ToLower().Contains(normalizedSearch))
                || (allowCity && a.City != null && a.City.ToLower().Contains(normalizedSearch))
                || (allowStatus && a.Status != null && a.Status.ToLower().Contains(normalizedSearch))
                || (a.Landlord != null
                    && a.Landlord.LandlordNavigation != null
                    && a.Landlord.LandlordNavigation.FullName != null
                    && a.Landlord.LandlordNavigation.FullName.ToLower().Contains(normalizedSearch)));
        }
    }
}