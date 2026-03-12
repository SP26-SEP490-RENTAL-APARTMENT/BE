using System.Linq.Expressions;
using DAL.Data;
using DAL.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repository.Implements
{
    public class Repository<T> : IRepository<T>
        where T : class
    {
        protected readonly AppDbContext _context;
        protected readonly DbSet<T> _dbSet;

        public Repository(AppDbContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }

        public virtual async Task<T?> GetByIdAsync(Guid id)
        {
            return await _dbSet.FindAsync(id);
        }

        public virtual async Task<(IEnumerable<T> Items, int TotalCount)> GetAllAsync(
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null,
            string? search = null,
            Dictionary<string, string>? filters = null,
            IEnumerable<string>? allowedColumns = null
        )
        {
            var query = _dbSet.AsQueryable();

            query = ApplyFilters(query, filters, allowedColumns);
            query = ApplySearch(query, search, allowedColumns);
            query = ApplySorting(query, sortBy, sortOrder);

            var totalCount = await query.CountAsync();
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return (items, totalCount);
        }

        protected IQueryable<T> ApplyFilters(
            IQueryable<T> query,
            Dictionary<string, string>? filters,
            IEnumerable<string>? allowedColumns
        )
        {
            if (filters != null && allowedColumns != null)
            {
                foreach (var filter in filters)
                {
                    var allowedCol = allowedColumns.FirstOrDefault(c =>
                        string.Equals(c, filter.Key, StringComparison.OrdinalIgnoreCase)
                    );
                    if (allowedCol != null)
                    {
                        var property = typeof(T).GetProperty(allowedCol);
                        if (property != null)
                        {
                            var parameter = Expression.Parameter(typeof(T), "e");
                            var propertyAccess = Expression.Property(parameter, property);
                            Expression equals;
                            if (property.PropertyType == typeof(string))
                            {
                                var toLowerMethod = typeof(string).GetMethod(
                                    "ToLower",
                                    Type.EmptyTypes
                                );
                                var propertyToLower = Expression.Call(
                                    propertyAccess,
                                    toLowerMethod!
                                );
                                var filterValue = Expression.Constant(
                                    filter.Value.ToLower(),
                                    typeof(string)
                                );
                                equals = Expression.Equal(propertyToLower, filterValue);
                            }
                            else if (property.PropertyType.IsEnum)
                            {
                                var enumValue = Enum.Parse(
                                    property.PropertyType,
                                    filter.Value,
                                    true
                                );
                                var filterValue = Expression.Constant(enumValue);
                                equals = Expression.Equal(propertyAccess, filterValue);
                            }
                            else if (
                                property.PropertyType == typeof(Guid)
                                || property.PropertyType == typeof(Guid?)
                            )
                            {
                                var guidValue = Guid.Parse(filter.Value);
                                var filterValue = Expression.Constant(
                                    guidValue,
                                    property.PropertyType
                                );
                                equals = Expression.Equal(propertyAccess, filterValue);
                            }
                            else
                            {
                                var filterValue = Expression.Constant(
                                    Convert.ChangeType(filter.Value, property.PropertyType)
                                );
                                equals = Expression.Equal(propertyAccess, filterValue);
                            }
                            var lambda = Expression.Lambda<Func<T, bool>>(equals, parameter);
                            query = query.Where(lambda);
                        }
                    }
                }
            }
            return query;
        }

        protected IQueryable<T> ApplySearch(
            IQueryable<T> query,
            string? search,
            IEnumerable<string>? allowedColumns
        )
        {
            if (!string.IsNullOrWhiteSpace(search) && allowedColumns != null)
            {
                Expression? searchExpression = null;
                var parameter = Expression.Parameter(typeof(T), "e");
                var toLowerMethod = typeof(string).GetMethod("ToLower", Type.EmptyTypes);
                var searchValue = Expression.Constant(search.ToLower());
                foreach (var col in allowedColumns)
                {
                    var property = typeof(T).GetProperty(col);
                    if (property != null && property.PropertyType == typeof(string))
                    {
                        var propertyAccess = Expression.Property(parameter, property);
                        var propertyToLower = Expression.Call(propertyAccess, toLowerMethod!);
                        var containsMethod = typeof(string).GetMethod("Contains", [typeof(string)]);
                        var contains = Expression.Call(
                            propertyToLower,
                            containsMethod!,
                            searchValue
                        );
                        searchExpression =
                            searchExpression == null
                                ? contains
                                : Expression.OrElse(searchExpression, contains);
                    }
                }
                if (searchExpression != null)
                {
                    var lambda = Expression.Lambda<Func<T, bool>>(searchExpression, parameter);
                    query = query.Where(lambda);
                }
            }
            return query;
        }

        protected IQueryable<T> ApplySorting(IQueryable<T> query, string? sortBy, string? sortOrder)
        {
            if (!string.IsNullOrEmpty(sortBy))
            {
                var prop = typeof(T)
                    .GetProperties()
                    .FirstOrDefault(p =>
                        string.Equals(p.Name, sortBy, StringComparison.OrdinalIgnoreCase)
                    );
                if (prop != null)
                {
                    var actualSortBy = prop.Name;
                    if (string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase))
                        query = query.OrderByDescending(e => EF.Property<object>(e, actualSortBy));
                    else
                        query = query.OrderBy(e => EF.Property<object>(e, actualSortBy));
                }
            }
            else
            {
                query = query.OrderBy(e => EF.Property<object>(e, "Id"));
            }
            return query;
        }

        public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.Where(predicate).ToListAsync();
        }

        public virtual async Task AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
        }

        public virtual void Update(T entity)
        {
            _dbSet.Update(entity);
        }

        public virtual void Remove(T entity)
        {
            _dbSet.Remove(entity);
        }

        public virtual async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }
    }

}
