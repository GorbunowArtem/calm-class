namespace CalmClass.Application.Common.Interfaces;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
    DateTime GetZonedTime(string timeZoneId);
    bool IsQuietHours(int startHour = 20, int endHour = 8, string timeZoneId = "Europe/Kyiv");
}
