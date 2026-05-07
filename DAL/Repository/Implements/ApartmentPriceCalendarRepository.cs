using DAL.Data;
using DAL.Models;
using DAL.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repository.Implements
{
    public class ApartmentPriceCalendarRepository : Repository<ApartmentPriceCalendar>, IApartmentPriceCalendarRepository
    {
        public ApartmentPriceCalendarRepository(AppDbContext context) : base(context)
        {
        }

        public void AddRange(IEnumerable<ApartmentPriceCalendar> newRecords)
        {
            _dbSet.AddRange(newRecords);
        }

        public void DeleteRange(IEnumerable<ApartmentPriceCalendar> recordsToDelete)
        {
            _dbSet.RemoveRange(recordsToDelete);
        }

        public async Task<IReadOnlyList<ApartmentPriceCalendar>> GetExistingManualRecords(Guid apartmentId, DateOnly start, DateOnly end)
        {
            var standardRecords = await _context.Set<ApartmentPriceCalendar>()
                .Where(p =>
                    p.ApartmentId == apartmentId &&
                    p.StartDate <= end &&
                    p.EndDate >= start)
                .AsNoTracking()
                .ToListAsync();

            return standardRecords;
        }

        public async Task<IEnumerable<ApartmentPriceCalendar>> GetExistingPriceCalendarRecordsAsync(
            Guid apartmentId, DateOnly startDate, DateOnly endDate)
        {
            var records = await _context.Set<ApartmentPriceCalendar>()
                .AsNoTracking()
                .Where(r =>
                    r.ApartmentId == apartmentId &&
                    r.EndDate >= startDate &&
                    r.StartDate <= endDate
                )
                .OrderBy(r => r.StartDate)
                .ToListAsync();

            return records;
        }

        public async Task<IReadOnlyList<ApartmentPriceCalendar>> GetExistingStandardRecords(Guid apartmentId, DateOnly start, DateOnly end)
        {
            var standardRecords = await _context.Set<ApartmentPriceCalendar>()
            .Where(p =>
                p.ApartmentId == apartmentId &&
                p.PriceType == "base" &&
                p.StartDate <= end &&
                p.EndDate >= start)
            .AsNoTracking()
            .ToListAsync();

            return standardRecords;
        }

        public async Task<IEnumerable<ApartmentPriceCalendar>> GetPriceCalendarRecordsForDeletionAsync(
    Guid apartmentId, DateOnly startDate, DateOnly endDate)
        {
            return await _dbSet
                .IgnoreQueryFilters() 
                .Where(r => r.ApartmentId == apartmentId &&
                            r.StartDate <= endDate &&
                            r.EndDate >= startDate)
                .ToListAsync();
        }
    }
}