namespace Cashflow.SharedKernel.Time;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
    DateOnly TodayInBusinessZone { get; }
    TimeZoneInfo BusinessZone { get; }
}
