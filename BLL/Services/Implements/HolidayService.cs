using BLL.Services.Interfaces;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements;

public class HolidayService : IHolidayService
{
    private readonly IHolidaysEventRepository _holidaysRepo;

    public HolidayService(IHolidaysEventRepository holidaysRepo)
    {
        _holidaysRepo = holidaysRepo;
    }

    public async Task<bool> IsHolidayAsync(DateOnly date, string? locationScope = null)
    {
        // Exact matching events
        var matches = await _holidaysRepo.FindAsync(h =>
            h.StartDate <= date && h.EndDate >= date &&
            (locationScope == null || h.LocationScope == locationScope));

        if (matches.Any()) return true;

        // Recurring events (simple year-based recurrence): match month/day
        var recurring = await _holidaysRepo.FindAsync(h => h.IsRecurring == true);
        foreach (var ev in recurring)
        {
            if (ev.StartDate.Month == date.Month && ev.StartDate.Day == date.Day)
            {
                if (locationScope == null || ev.LocationScope == null || ev.LocationScope == locationScope)
                    return true;
            }
        }

        return false;
    }
}
