using System.Linq.Expressions;

namespace DAL.Repository.Interfaces
{
    public interface IRepository<T>
        where T : class
    {
        Task<T?> GetByIdAsync(Guid id);
        Task<(IEnumerable<T> Items, int TotalCount)> GetAllAsync(
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null,
            string? search = null,
            Dictionary<string, string>? filters = null,
            IEnumerable<string>? allowedColumns = null
        );
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
        async Task<(IEnumerable<T> Items, int TotalCount)> FindPagedAsync(
            Expression<Func<T, bool>> predicate,
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null)
        {
            // Default fallback: execute full FindAsync and page in-memory. Implementations may override for DB-level paging.
            var items = await FindAsync(predicate).ConfigureAwait(false);
            var list = items.ToList();
            var total = list.Count;
            var pageItems = list.Skip((page - 1) * pageSize).Take(pageSize);
            return (pageItems, total);
        }
        Task AddAsync(T entity);
        void Update(T entity);
        void Remove(T entity);
        Task<int> SaveChangesAsync();
        Task<IEnumerable<T>> FindNoTrackingAsync(Expression<Func<T, bool>> predicate);
    }
}
