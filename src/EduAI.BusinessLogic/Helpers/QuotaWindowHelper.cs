using EduAI.Model.Entities;

namespace EduAI.BusinessLogic.Helpers;

public static class QuotaWindowHelper
{
    /// <summary>Daily quota window start in UTC, using SystemSettings timezone + reset hour.</summary>
    public static DateTime GetWindowStartUtc(SystemSettings settings)
        => GetDayWindowStartUtc(settings);

    public static DateTime GetDayWindowStartUtc(SystemSettings settings)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(settings.DefaultTimezone);
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            var windowStartLocal = localNow.Date.AddHours(settings.DailyQuotaResetHour);
            if (localNow < windowStartLocal)
                windowStartLocal = windowStartLocal.AddDays(-1);

            return TimeZoneInfo.ConvertTimeToUtc(windowStartLocal, tz);
        }
        catch (TimeZoneNotFoundException)
        {
            return FallbackDayWindowUtc(settings.DailyQuotaResetHour);
        }
        catch (InvalidTimeZoneException)
        {
            return FallbackDayWindowUtc(settings.DailyQuotaResetHour);
        }
    }

    /// <summary>Monthly quota window start in UTC, aligned to calendar month in SystemSettings timezone.</summary>
    public static DateTime GetMonthWindowStartUtc(SystemSettings settings)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(settings.DefaultTimezone);
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            var monthStartLocal = new DateTime(localNow.Year, localNow.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(monthStartLocal, tz);
        }
        catch (TimeZoneNotFoundException)
        {
            var utcNow = DateTime.UtcNow;
            return new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        }
        catch (InvalidTimeZoneException)
        {
            var utcNow = DateTime.UtcNow;
            return new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        }
    }

    private static DateTime FallbackDayWindowUtc(int resetHour)
    {
        var resetUtc = DateTime.UtcNow.Date.AddHours(resetHour);
        return DateTime.UtcNow < resetUtc ? resetUtc.AddDays(-1) : resetUtc;
    }
}
