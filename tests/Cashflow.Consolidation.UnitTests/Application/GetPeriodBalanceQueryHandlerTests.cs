using Cashflow.Consolidation.Application.Abstractions;
using Cashflow.Consolidation.Application.Balances;
using Cashflow.Consolidation.Application.Balances.Queries.GetPeriodBalance;
using Cashflow.Consolidation.Domain.Projections;
using NSubstitute;

namespace Cashflow.Consolidation.UnitTests.Application;

public sealed class GetPeriodBalanceQueryHandlerTests
{
    private readonly Guid _merchantId = Guid.Parse("0193e7a8-d8f0-7c5e-9b21-3f9f8a4d1c00");

    [Fact]
    public async Task From_after_to_returns_invalid_range_error()
    {
        var repo = Substitute.For<IDailyBalanceReadRepository>();
        var sut = new GetPeriodBalanceQueryHandler(repo);

        var result = await sut.Handle(
            new GetPeriodBalanceQuery(_merchantId, new DateOnly(2026, 5, 14), new DateOnly(2026, 5, 1)),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BalanceErrors.InvalidRange);
        await repo.DidNotReceive().GetRangeAsync(
            Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Range_wider_than_31_days_returns_range_too_wide()
    {
        var repo = Substitute.For<IDailyBalanceReadRepository>();
        var sut = new GetPeriodBalanceQueryHandler(repo);

        var result = await sut.Handle(
            new GetPeriodBalanceQuery(_merchantId, new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 5)),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BalanceErrors.RangeTooWide);
    }

    [Fact]
    public async Task Valid_range_aggregates_totals_across_days()
    {
        var repo = Substitute.For<IDailyBalanceReadRepository>();
        var from = new DateOnly(2026, 5, 1);
        var to = new DateOnly(2026, 5, 3);
        var sample = new List<DailyBalanceReadModel>
        {
            MakeDay(from, credits: 100m, debits: 30m, count: 2),
            MakeDay(from.AddDays(1), credits: 200m, debits: 70m, count: 3),
            MakeDay(to, credits: 50m, debits: 0m, count: 1),
        };
        repo.GetRangeAsync(_merchantId, from, to, Arg.Any<CancellationToken>()).Returns(sample);

        var sut = new GetPeriodBalanceQueryHandler(repo);
        var result = await sut.Handle(new GetPeriodBalanceQuery(_merchantId, from, to), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCredits.Should().Be(350m);
        result.Value.TotalDebits.Should().Be(100m);
        result.Value.Balance.Should().Be(250m);
        result.Value.EntriesCount.Should().Be(6);
        result.Value.Days.Should().HaveCount(3);
    }

    [Fact]
    public async Task Valid_range_with_no_data_returns_zero_totals_and_empty_days()
    {
        var repo = Substitute.For<IDailyBalanceReadRepository>();
        var from = new DateOnly(2026, 5, 1);
        var to = new DateOnly(2026, 5, 5);
        repo.GetRangeAsync(_merchantId, from, to, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<DailyBalanceReadModel>());

        var sut = new GetPeriodBalanceQueryHandler(repo);
        var result = await sut.Handle(new GetPeriodBalanceQuery(_merchantId, from, to), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCredits.Should().Be(0m);
        result.Value.TotalDebits.Should().Be(0m);
        result.Value.Balance.Should().Be(0m);
        result.Value.Days.Should().BeEmpty();
    }

    private DailyBalanceReadModel MakeDay(DateOnly date, decimal credits, decimal debits, int count) =>
        new(
            MerchantId: _merchantId,
            Date: date,
            TotalCredits: credits,
            TotalDebits: debits,
            Balance: credits - debits,
            EntriesCount: count,
            ByCategory: Array.Empty<CategoryBucket>(),
            LastUpdatedAt: new DateTimeOffset(2026, 5, 14, 12, 0, 0, TimeSpan.Zero),
            Revision: 1L,
            LastAppliedEventId: Guid.NewGuid());
}
