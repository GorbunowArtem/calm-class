namespace CalmClass.Application.Common.Options;

public record QuietHoursOptions
{
    public const string SectionName = "QuietHours";

    public int StartHour { get; init; } = 20; // 20:00 (8 PM) Kyiv time
    public int EndHour { get; init; } = 8;     // 08:00 (8 AM) Kyiv time
    public string TimeZoneId { get; init; } = "Europe/Kyiv";
}
