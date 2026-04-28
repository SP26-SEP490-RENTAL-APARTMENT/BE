public static class DateHelper
{
    public static DayOfWeek GetDayOfWeekFromDate(DateOnly date)
    {
        // DayOfWeek enum is Culture-dependent, but this conversion is reliable.
        return date.DayOfWeek;
    }

    public static DateOnly Max(DateOnly date1, DateOnly date2)
    {
        return date1 > date2 ? date1 : date2;
    }

    public static DateOnly Min(DateOnly date1, DateOnly date2)
    {
        return date1 < date2 ? date1 : date2;
    }

    public static DateTime AddDay(this DateTime date)
    {
        return date.AddDays(1).Date;
    }
}