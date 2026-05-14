namespace Cashflow.SharedKernel.Time;

public sealed class SystemClock : IClock
{
    // .NET 8+ resolves IANA <-> Windows TZ IDs automatically; the fallback covers
    // older runtimes or stripped images without ICU/tzdata. Containers use tzdata,
    // so the IANA lookup succeeds there too.
    private static readonly TimeZoneInfo BusinessZoneInstance = LoadBusinessZone();

    private static TimeZoneInfo LoadBusinessZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
        }
    }

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public TimeZoneInfo BusinessZone => BusinessZoneInstance;

    public DateOnly TodayInBusinessZone =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(UtcNow, BusinessZone).DateTime);
}
