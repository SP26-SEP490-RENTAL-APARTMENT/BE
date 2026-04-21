using DAL.Data;
using DAL.Models;
using DAL.Repository.Interfaces;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repository.Implements
{
    public class PropertyInspectionRepository : Repository<PropertyInspection>, IPropertyInspectionRepository
    {
        public PropertyInspectionRepository(AppDbContext context) : base(context)
        {
        }

        public override async Task<PropertyInspection?> GetByIdAsync(Guid id)
        {
            return await _dbSet
                .Include(i => i.Apartment)
                .FirstOrDefaultAsync(i => i.InspectionId == id);
        }

        public override async Task<(IEnumerable<PropertyInspection> Items, int TotalCount)> GetAllAsync(
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null,
            string? search = null,
            Dictionary<string, string>? filters = null,
            IEnumerable<string>? allowedColumns = null
        )
        {
            var query = _dbSet
                .Include(i => i.Apartment)
                .AsQueryable();

            query = ApplyFilters(query, filters, allowedColumns);
            query = ApplyApartmentNameFilter(query, filters);
            query = ApplyApartmentNameSearch(query, search, allowedColumns);
            query = ApplySorting(query, sortBy, sortOrder);

            var totalCount = await query.CountAsync();
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return (items, totalCount);
        }

        private static IQueryable<PropertyInspection> ApplyApartmentNameFilter(
            IQueryable<PropertyInspection> query,
            Dictionary<string, string>? filters)
        {
            if (filters == null
                || !filters.TryGetValue("ApartmentName", out var apartmentName)
                || string.IsNullOrWhiteSpace(apartmentName))
            {
                return query;
            }

            var normalizedApartmentName = apartmentName.Trim().ToLower();
            return query.Where(i =>
                i.Apartment != null
                && i.Apartment.Title != null
                && i.Apartment.Title.ToLower().Contains(normalizedApartmentName));
        }

        private IQueryable<PropertyInspection> ApplyApartmentNameSearch(
            IQueryable<PropertyInspection> query,
            string? search,
            IEnumerable<string>? allowedColumns)
        {
            if (string.IsNullOrWhiteSpace(search) || allowedColumns == null)
            {
                return query;
            }

            Expression? searchExpression = null;
            var parameter = Expression.Parameter(typeof(PropertyInspection), "e");
            var toLowerMethod = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes);
            var containsMethod = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)]);
            var searchValue = Expression.Constant(search.Trim().ToLower());

            foreach (var col in allowedColumns)
            {
                if (string.Equals(col, "ApartmentName", StringComparison.OrdinalIgnoreCase))
                {
                    var apartmentProperty = Expression.Property(parameter, nameof(PropertyInspection.Apartment));
                    var apartmentNotNull = Expression.NotEqual(
                        apartmentProperty,
                        Expression.Constant(null, typeof(Apartment)));

                    var apartmentTitle = Expression.Property(apartmentProperty, nameof(Apartment.Title));
                    var titleNotNull = Expression.NotEqual(
                        apartmentTitle,
                        Expression.Constant(null, typeof(string)));
                    var titleToLower = Expression.Call(apartmentTitle, toLowerMethod!);
                    var titleContains = Expression.Call(titleToLower, containsMethod!, searchValue);
                    var apartmentNameMatch = Expression.AndAlso(
                        apartmentNotNull,
                        Expression.AndAlso(titleNotNull, titleContains));

                    searchExpression = searchExpression == null
                        ? apartmentNameMatch
                        : Expression.OrElse(searchExpression, apartmentNameMatch);
                    continue;
                }

                var property = typeof(PropertyInspection).GetProperty(col);
                if (property != null && property.PropertyType == typeof(string))
                {
                    var propertyAccess = Expression.Property(parameter, property);
                    var propertyToLower = Expression.Call(propertyAccess, toLowerMethod!);
                    var contains = Expression.Call(propertyToLower, containsMethod!, searchValue);
                    searchExpression = searchExpression == null
                        ? contains
                        : Expression.OrElse(searchExpression, contains);
                }
            }

            if (searchExpression != null)
            {
                var lambda = Expression.Lambda<Func<PropertyInspection, bool>>(searchExpression, parameter);
                query = query.Where(lambda);
            }

            return query;
        }
    }
}