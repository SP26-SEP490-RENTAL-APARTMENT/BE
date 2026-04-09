using System.Globalization;

namespace Common.Utils
{
    public static class VietnamTime
    {
        private const string IanaTimeZoneId = "Asia/Ho_Chi_Minh";
        private const string WindowsTimeZoneId = "SE Asia Standard Time";

        public static readonly TimeSpan Offset = TimeSpan.FromHours(7);

        public static TimeZoneInfo TimeZone { get; } = ResolveTimeZone();

        public static DateTime Now => ToVietnamTime(DateTime.UtcNow);

        public static DateTime TodayDateTime => Now.Date;

        public static DateOnly Today => DateOnly.FromDateTime(Now);

        public static DateTime ToVietnamTime(DateTime value)
        {
            if (value == DateTime.MinValue || value == DateTime.MaxValue)
            {
                return value;
            }

            if (value.Kind == DateTimeKind.Unspecified)
            {
                return value;
            }

            var converted = value.Kind == DateTimeKind.Utc
                ? TimeZoneInfo.ConvertTimeFromUtc(value, TimeZone)
                : TimeZoneInfo.ConvertTime(value, TimeZoneInfo.Local, TimeZone);

            return DateTime.SpecifyKind(converted, DateTimeKind.Unspecified);
        }

        public static DateTimeOffset ToVietnamOffset(DateTime value)
        {
            var vietnamTime = ToVietnamTime(value);
            return new DateTimeOffset(vietnamTime, Offset);
        }

        public static DateTime ParseToVietnamTime(string value)
        {
            if (DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var dto
            ))
            {
                var vietnamTime = TimeZoneInfo.ConvertTime(dto, TimeZone);
                return DateTime.SpecifyKind(vietnamTime.DateTime, DateTimeKind.Unspecified);
            }

            if (DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var dt
            ))
            {
                return ToVietnamTime(dt);
            }

            throw new FormatException($"Invalid datetime value: {value}");
        }

        private static TimeZoneInfo ResolveTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(IanaTimeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById(WindowsTimeZoneId);
            }
            catch (InvalidTimeZoneException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById(WindowsTimeZoneId);
            }
        }
    }
}
