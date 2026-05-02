using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using DAL.Repository.Interfaces;

namespace BLL.Tests
{
    internal class InMemoryRepository<T> : IRepository<T>
        where T : class
    {
        private readonly List<T> _items = new();
        public List<T> Items => _items;
        private readonly Func<T, Guid> _keySelector;

        public InMemoryRepository(Func<T, Guid>? keySelector = null, params T[] items)
        {
            if (keySelector != null)
            {
                _keySelector = keySelector;
            }
            else
            {
                _keySelector = InferKeySelector();
            }

            if (items != null && items.Length > 0)
            {
                _items.AddRange(items);
            }
        }

        private Func<T, Guid> InferKeySelector()
        {
            return (T t) =>
            {
                var type = typeof(T);
                var prop = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(p => p.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase) && p.PropertyType == typeof(Guid));
                if (prop == null)
                    throw new InvalidOperationException($"No Guid Id-like property found on {type.FullName}. Provide a key selector.");
                return (Guid)(prop.GetValue(t) ?? Guid.Empty);
            };
        }

        public Task AddAsync(T entity)
        {
            _items.Add(entity);
            return Task.CompletedTask;
        }

        public Task<(IEnumerable<T> Items, int TotalCount)> GetAllAsync(int page, int pageSize, string? sortBy = null, string? sortOrder = null, string? search = null, Dictionary<string, string>? filters = null, IEnumerable<string>? allowedColumns = null)
        {
            return Task.FromResult((_items.AsEnumerable(), _items.Count));
        }

        public Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            var compiled = predicate.Compile();
            var result = _items.Where(compiled);
            return Task.FromResult(result.AsEnumerable());
        }

        public Task<T?> GetByIdAsync(Guid id)
        {
            var item = _items.FirstOrDefault(i => _keySelector(i) == id);
            return Task.FromResult(item);
        }

        public void Remove(T entity)
        {
            _items.Remove(entity);
        }

        public void Update(T entity)
        {
            // no-op for in-memory
        }

        public Task<int> SaveChangesAsync()
        {
            return Task.FromResult(1);
        }
    }
}
