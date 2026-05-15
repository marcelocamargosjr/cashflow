using Cashflow.Consolidation.Application.Abstractions;
using Cashflow.Consolidation.Application.Balances.Queries.GetDailyBalance;
using Cashflow.Consolidation.Domain.Projections;
using Cashflow.SharedKernel.Time;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Cashflow.Consolidation.UnitTests.Application;

public sealed class GetDailyBalanceQueryHandlerTests
{
    private readonly Guid _merchantId = Guid.Parse("0193e7a8-d8f0-7c5e-9b21-3f9f8a4d1c00");
    private readonly DateOnly _date = new(2026, 5, 14);
    private readonly DateTimeOffset _now = new(2026, 5, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Cache_hit_returns_dto_with_hit_true_and_does_not_call_repository()
    {
        var cache = Substitute.For<IDailyBalanceCache>();
        var repo = Substitute.For<IDailyBalanceReadRepository>();
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(_now);

        var model = SampleModel(lastUpdated: _now.AddSeconds(-20));
        cache.TryGetAsync(_merchantId, _date, Arg.Any<CancellationToken>())
            .Returns(model);

        var sut = new GetDailyBalanceQueryHandler(cache, repo, clock, NullLogger<GetDailyBalanceQueryHandler>.Instance);

        var result = await sut.Handle(new GetDailyBalanceQuery(_merchantId, _date), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Cache.Hit.Should().BeTrue();
        result.Value.Cache.AgeSeconds.Should().Be(20);
        await repo.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cache_miss_with_lock_won_reads_repo_and_populates_cache()
    {
        var cache = Substitute.For<IDailyBalanceCache>();
        var repo = Substitute.For<IDailyBalanceReadRepository>();
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(_now);

        cache.TryGetAsync(_merchantId, _date, Arg.Any<CancellationToken>()).Returns((DailyBalanceReadModel?)null);
        cache.TryAcquireStampedeLockAsync(_merchantId, _date, Arg.Any<CancellationToken>()).Returns("lock-token");
        var fresh = SampleModel(lastUpdated: _now);
        repo.GetAsync(_merchantId, _date, Arg.Any<CancellationToken>()).Returns(fresh);

        var sut = new GetDailyBalanceQueryHandler(cache, repo, clock, NullLogger<GetDailyBalanceQueryHandler>.Instance);

        var result = await sut.Handle(new GetDailyBalanceQuery(_merchantId, _date), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Cache.Hit.Should().BeFalse();
        result.Value.TotalCredits.Should().Be(150m);
        await cache.Received(1).SetAsync(fresh, Arg.Any<CancellationToken>());
        await cache.Received(1).ReleaseStampedeLockAsync(_merchantId, _date, "lock-token", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cache_miss_when_repo_returns_null_returns_empty_dto_without_populating_cache()
    {
        var cache = Substitute.For<IDailyBalanceCache>();
        var repo = Substitute.For<IDailyBalanceReadRepository>();
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(_now);

        cache.TryGetAsync(_merchantId, _date, Arg.Any<CancellationToken>()).Returns((DailyBalanceReadModel?)null);
        cache.TryAcquireStampedeLockAsync(_merchantId, _date, Arg.Any<CancellationToken>()).Returns("lock-token");
        repo.GetAsync(_merchantId, _date, Arg.Any<CancellationToken>()).Returns((DailyBalanceReadModel?)null);

        var sut = new GetDailyBalanceQueryHandler(cache, repo, clock, NullLogger<GetDailyBalanceQueryHandler>.Instance);

        var result = await sut.Handle(new GetDailyBalanceQuery(_merchantId, _date), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.EntriesCount.Should().Be(0);
        result.Value.TotalCredits.Should().Be(0m);
        result.Value.ByCategory.Should().BeEmpty();
        await cache.DidNotReceive().SetAsync(Arg.Any<DailyBalanceReadModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cache_miss_with_lock_lost_then_repopulated_returns_hit()
    {
        var cache = Substitute.For<IDailyBalanceCache>();
        var repo = Substitute.For<IDailyBalanceReadRepository>();
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(_now);

        var populated = SampleModel(lastUpdated: _now.AddSeconds(-5));
        cache.TryGetAsync(_merchantId, _date, Arg.Any<CancellationToken>())
            .Returns((DailyBalanceReadModel?)null, populated);
        cache.TryAcquireStampedeLockAsync(_merchantId, _date, Arg.Any<CancellationToken>()).Returns((string?)null);

        var sut = new GetDailyBalanceQueryHandler(cache, repo, clock, NullLogger<GetDailyBalanceQueryHandler>.Instance);

        var result = await sut.Handle(new GetDailyBalanceQuery(_merchantId, _date), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Cache.Hit.Should().BeTrue();
        await repo.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cache_miss_with_lock_lost_and_still_empty_falls_through_to_repo_without_release()
    {
        var cache = Substitute.For<IDailyBalanceCache>();
        var repo = Substitute.For<IDailyBalanceReadRepository>();
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(_now);

        cache.TryGetAsync(_merchantId, _date, Arg.Any<CancellationToken>())
            .Returns((DailyBalanceReadModel?)null, (DailyBalanceReadModel?)null);
        cache.TryAcquireStampedeLockAsync(_merchantId, _date, Arg.Any<CancellationToken>()).Returns((string?)null);
        var fresh = SampleModel(lastUpdated: _now);
        repo.GetAsync(_merchantId, _date, Arg.Any<CancellationToken>()).Returns(fresh);

        var sut = new GetDailyBalanceQueryHandler(cache, repo, clock, NullLogger<GetDailyBalanceQueryHandler>.Instance);

        var result = await sut.Handle(new GetDailyBalanceQuery(_merchantId, _date), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Cache.Hit.Should().BeFalse();
        await cache.DidNotReceive().ReleaseStampedeLockAsync(
            Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private DailyBalanceReadModel SampleModel(DateTimeOffset lastUpdated)
    {
        var buckets = new List<CategoryBucket>
        {
            new("Sales", 150m, 0m, 1)
        };
        return new DailyBalanceReadModel(
            MerchantId: _merchantId,
            Date: _date,
            TotalCredits: 150m,
            TotalDebits: 0m,
            Balance: 150m,
            EntriesCount: 1,
            ByCategory: buckets,
            LastUpdatedAt: lastUpdated,
            Revision: 1L,
            LastAppliedEventId: Guid.NewGuid());
    }
}
