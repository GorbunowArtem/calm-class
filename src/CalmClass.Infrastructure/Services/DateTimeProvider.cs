namespace CalmClass.Infrastructure.Services;

using CalmClass.Application.Common.Interfaces;

public class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;

    public DateTime GetZonedTime(string timeZoneId)
    {
        var tz = ResolveTimeZone(timeZoneId);
        return TimeZoneInfo.ConvertTimeFromUtc(UtcNow, tz);
    }

    public bool IsQuietHours(int startHour = 20, int endHour = 8, string timeZoneId = "Europe/Kyiv")
    {
        var localTime = GetZonedTime(timeZoneId);
        var hour = localTime.Hour;

        // E.g., startHour = 20, endHour = 8:
        // Quiet if hour >= 20 OR hour < 8
        if (startHour > endHour)
        {
            return hour >= startHour || hour < endHour;
        }

        // Standard daytime quiet hours (if ever configured start < end)
        return hour >= startHour && hour < endHour;
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            // Windows fallback for Europe/Kyiv
            if (timeZoneId.Equals("Europe/Kyiv", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time");
                }
                catch
                {
                    // Fallback to UTC+2 / +3
                }
            }

            return TimeZoneInfo.Utc;
        }
    }
}
