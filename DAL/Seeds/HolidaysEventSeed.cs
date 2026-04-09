using DAL.Data;
using DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace DAL.Seeds;

public static class HolidaysEventSeed
{
    public static async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        var currentYear = Common.Utils.VietnamTime.Now.Year;

        var events = new List<HolidaysEvent>
        {
            new()
            {
                EventName = "New Year",
                EventType = "national_holiday",
                StartDate = new DateOnly(currentYear, 1, 1),
                EndDate = new DateOnly(currentYear, 1, 1),
                LocationScope = "VN",
                Description = "New Year holiday",
                IsRecurring = true,
                RecurrenceRule = "FREQ=YEARLY;BYMONTH=1;BYMONTHDAY=1",
                CreatedAt = Common.Utils.VietnamTime.Now,
                UpdatedAt = Common.Utils.VietnamTime.Now
            },
            new()
            {
                EventName = "National Day",
                EventType = "national_holiday",
                StartDate = new DateOnly(currentYear, 9, 2),
                EndDate = new DateOnly(currentYear, 9, 2),
                LocationScope = "VN",
                Description = "Vietnam National Day",
                IsRecurring = true,
                RecurrenceRule = "FREQ=YEARLY;BYMONTH=9;BYMONTHDAY=2",
                CreatedAt = Common.Utils.VietnamTime.Now,
                UpdatedAt = Common.Utils.VietnamTime.Now
            }
        };

        foreach (var holiday in events)
        {
            var exists = await context.HolidaysEvents.AnyAsync(
                h => h.EventName == holiday.EventName && h.StartDate == holiday.StartDate,
                cancellationToken);

            if (exists)
            {
                continue;
            }

            holiday.EventId = Guid.NewGuid();
            context.HolidaysEvents.Add(holiday);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
