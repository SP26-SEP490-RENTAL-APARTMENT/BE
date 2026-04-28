using DAL.Models;
using DAL.Repository.Interfaces;

namespace DAL.Repository.Interfaces
{
    public interface IApartmentPriceCalendarRepository : IRepository<ApartmentPriceCalendar>
    {
        void AddRange(IEnumerable<ApartmentPriceCalendar> newRecords);
        void DeleteRange(IEnumerable<ApartmentPriceCalendar> recordsToDelete);
        Task<IReadOnlyList<ApartmentPriceCalendar>> GetExistingManualRecords(Guid apartmentId, DateOnly start, DateOnly end);
        Task<IEnumerable<ApartmentPriceCalendar>> GetExistingPriceCalendarRecordsAsync(Guid apartmentId, DateOnly startDate, DateOnly endDate);
        Task<IReadOnlyList<ApartmentPriceCalendar>> GetExistingStandardRecords(Guid apartmentId, DateOnly start, DateOnly end);
        Task<IEnumerable<ApartmentPriceCalendar>> GetPriceCalendarRecordsForDeletionAsync(Guid apartmentId, DateOnly startDate, DateOnly endDate);
    }
}