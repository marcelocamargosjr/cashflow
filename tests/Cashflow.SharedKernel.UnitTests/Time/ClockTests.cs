using Cashflow.SharedKernel.Time;

namespace Cashflow.SharedKernel.UnitTests.Time;

public class ClockTests
{
    [Fact]
    public void MockedClock_ShouldReturnFixedDate()
    {
        var fixedInstant = new DateTimeOffset(2026, 5, 14, 3, 30, 0, TimeSpan.Zero); // 00:30 BRT
        var clock = new FakeClock(fixedInstant);

        clock.UtcNow.Should().Be(fixedInstant);
        clock.TodayInBusinessZone.Should().Be(new DateOnly(2026, 5, 14));
    }

    [Fact]
    public void MockedClock_AtEndOfBrtDay_ShouldBeLocalDayNotUtcDay()
    {
        // 2026-05-14 02:00 UTC == 2026-05-13 23:00 BRT (UTC-3).
        var instant = new DateTimeOffset(2026, 5, 14, 2, 0, 0, TimeSpan.Zero);
        var clock = new FakeClock(instant);

        clock.TodayInBusinessZone.Should().Be(new DateOnly(2026, 5, 13));
    }

    [Fact]
    public void SystemClock_BusinessZone_ShouldBeSaoPaulo()
    {
        var clock = new SystemClock();

        clock.BusinessZone.BaseUtcOffset.Should().Be(TimeSpan.FromHours(-3));
    }

    [Fact]
    public void SystemClock_UtcNow_ShouldBeNearWallClock()
    {
        var clock = new SystemClock();

        var before = DateTimeOffset.UtcNow.AddSeconds(-5);
        var observed = clock.UtcNow;
        var after = DateTimeOffset.UtcNow.AddSeconds(5);

        observed.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    private sealed class FakeClock : IClock
    {
        public FakeClock(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
            BusinessZone = LoadBusinessZone();
        }

        public DateTimeOffset UtcNow { get; }

        public TimeZoneInfo BusinessZone { get; }

        public DateOnly TodayInBusinessZone =>
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(UtcNow, BusinessZone).DateTime);

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
    }
}
