using System.Linq.Expressions;
using System.Globalization;
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
            if (filters == null || filters.Count == 0 || allowedColumns == null)
            {
                return query;
            }

            // Ignore entries like filters="", filters[Status]="", or empty keys.
            var effectiveFilters = filters
                .Where(f => !string.IsNullOrWhiteSpace(f.Key) && !string.IsNullOrWhiteSpace(f.Value))
                .ToList();

            if (effectiveFilters.Count == 0)
            {
                return query;
            }

            foreach (var filter in effectiveFilters)
            {
                var allowedCol = allowedColumns!.FirstOrDefault(c =>
                    string.Equals(c, filter.Key, StringComparison.OrdinalIgnoreCase));

                if (allowedCol == null)
                {
                    continue;
                }

                var property = typeof(T).GetProperty(allowedCol);

                if (property == null)
                {
                    continue;
                }

                if (filters != null && allowedColumns != null)
                {
                    allowedCol = allowedColumns.FirstOrDefault(c =>
                        string.Equals(c, filter.Key, StringComparison.OrdinalIgnoreCase)
                    );
                    if (allowedCol != null)
                    {
                        property = typeof(T).GetProperty(allowedCol);
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
                            else
                            {
                                if (!TryConvertFilterValue(property.PropertyType, filter.Value, out var convertedValue))
                                {
                                    // Ignore invalid filter values instead of failing the entire request.
                                    continue;
                                }

                                var filterValue = Expression.Constant(convertedValue, property.PropertyType);
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

        private static bool TryConvertFilterValue(Type propertyType, string rawValue, out object? convertedValue)
        {
            convertedValue = null;

            var isNullable = Nullable.GetUnderlyingType(propertyType) != null;
            var targetType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

            if (string.Equals(rawValue, "null", StringComparison.OrdinalIgnoreCase))
            {
                if (isNullable || !propertyType.IsValueType)
                {
                    convertedValue = null;
                    return true;
                }

                return false;
            }

            object? parsedValue;

            if (targetType == typeof(Guid))
            {
                if (!Guid.TryParse(rawValue, out var guidValue))
                    return false;

                parsedValue = guidValue;
            }
            else if (targetType.IsEnum)
            {
                if (!Enum.TryParse(targetType, rawValue, true, out var enumValue))
                    return false;

                parsedValue = enumValue;
            }
            else if (targetType == typeof(DateOnly))
            {
                if (!DateOnly.TryParse(rawValue, out var dateOnlyValue))
                    return false;

                parsedValue = dateOnlyValue;
            }
            else if (targetType == typeof(DateTime))
            {
                if (!DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind, out var dateTimeValue)
                    && !DateTime.TryParse(rawValue, out dateTimeValue))
                {
                    return false;
                }

                parsedValue = dateTimeValue;
            }
            else if (targetType == typeof(bool))
            {
                if (bool.TryParse(rawValue, out var boolValue))
                {
                    parsedValue = boolValue;
                }
                else if (rawValue == "1")
                {
                    parsedValue = true;
                }
                else if (rawValue == "0")
                {
                    parsedValue = false;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                try
                {
                    parsedValue = Convert.ChangeType(rawValue, targetType, CultureInfo.InvariantCulture);
                }
                catch
                {
                    return false;
                }
            }

            if (isNullable)
            {
                convertedValue = Activator.CreateInstance(propertyType, parsedValue!);
                return true;
            }

            convertedValue = parsedValue;
            return true;
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
            static string? ResolveDefaultSortPropertyName()
            {
                var type = typeof(T);

                var idProp = type.GetProperty("Id");
                if (idProp != null)
                    return idProp.Name;

                var typeIdName = type.Name + "Id";
                var typeIdProp = type.GetProperties().FirstOrDefault(p => string.Equals(p.Name, typeIdName, StringComparison.OrdinalIgnoreCase));
                if (typeIdProp != null)
                    return typeIdProp.Name;

                var guidIdProp = type.GetProperties().FirstOrDefault(p =>
                    p.PropertyType == typeof(Guid) && p.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase));
                if (guidIdProp != null)
                    return guidIdProp.Name;

                var anyIdProp = type.GetProperties().FirstOrDefault(p => p.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase));
                return anyIdProp?.Name;
            }

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
                var defaultSortBy = ResolveDefaultSortPropertyName();
                if (!string.IsNullOrEmpty(defaultSortBy))
                    query = query.OrderBy(e => EF.Property<object>(e, defaultSortBy));
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
            var entry = _context.Entry(entity);

            if (entry.State != EntityState.Detached)
            {
                entry.State = EntityState.Modified;
                return;
            }

            var entityType = _context.Model.FindEntityType(typeof(T));
            var primaryKey = entityType?.FindPrimaryKey();

            if (primaryKey != null)
            {
                var trackedEntity = _dbSet.Local.FirstOrDefault(local => HasSamePrimaryKey(local, entity, primaryKey.Properties));
                if (trackedEntity != null)
                {
                    _context.Entry(trackedEntity).CurrentValues.SetValues(entity);
                    return;
                }
            }

            _dbSet.Update(entity);
        }

        private static bool HasSamePrimaryKey(T localEntity, T detachedEntity, IReadOnlyList<Microsoft.EntityFrameworkCore.Metadata.IProperty> keyProperties)
        {
            foreach (var keyProperty in keyProperties)
            {
                var property = keyProperty.PropertyInfo;
                if (property == null)
                {
                    return false;
                }

                var localValue = property.GetValue(localEntity);
                var detachedValue = property.GetValue(detachedEntity);

                if (!Equals(localValue, detachedValue))
                {
                    return false;
                }
            }

            return true;
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
