using Cashflow.SharedKernel.Time;

namespace Cashflow.Ledger.UnitTests.TestDoubles;

public sealed class FakeClock(DateTimeOffset utcNow, DateOnly? today = null, TimeZoneInfo? zone = null) : IClock
{
    public DateTimeOffset UtcNow { get; } = utcNow;
    public DateOnly TodayInBusinessZone { get; } = today ?? DateOnly.FromDateTime(utcNow.UtcDateTime);
    public TimeZoneInfo BusinessZone { get; } = zone ?? TimeZoneInfo.Utc;
}
