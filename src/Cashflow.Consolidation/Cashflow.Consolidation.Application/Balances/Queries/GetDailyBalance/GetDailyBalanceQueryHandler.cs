using Cashflow.Consolidation.Application.Abstractions;
using Cashflow.Consolidation.Application.Balances.Dtos;
using Cashflow.SharedKernel.Results;
using Cashflow.SharedKernel.Time;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cashflow.Consolidation.Application.Balances.Queries.GetDailyBalance;

/// <summary>
/// Cache-aside read of the daily projection (Redis → Mongo → populate Redis).
/// Stampede-protected via a short-lived lock; on lock contention we wait briefly and
/// retry the cache before falling through to Mongo unguarded (graceful degradation).
/// </summary>
public sealed class GetDailyBalanceQueryHandler
    : IRequestHandler<GetDailyBalanceQuery, Result<DailyBalanceDto>>
{
    private static readonly TimeSpan StampedeWait = TimeSpan.FromMilliseconds(100);

    private readonly IDailyBalanceCache _cache;
    private readonly IDailyBalanceReadRepository _repository;
    private readonly IClock _clock;
    private readonly ILogger<GetDailyBalanceQueryHandler> _logger;

    public GetDailyBalanceQueryHandler(
        IDailyBalanceCache cache,
        IDailyBalanceReadRepository repository,
        IClock clock,
        ILogger<GetDailyBalanceQueryHandler> logger)
    {
        _cache = cache;
        _repository = repository;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<DailyBalanceDto>> Handle(
        GetDailyBalanceQuery request,
        CancellationToken cancellationToken)
    {
        var cached = await _cache.TryGetAsync(request.MerchantId, request.Date, cancellationToken)
            .ConfigureAwait(false);
        if (cached is not null)
            return BuildDto(cached, hit: true);

        var lockToken = await _cache
            .TryAcquireStampedeLockAsync(request.MerchantId, request.Date, cancellationToken)
            .ConfigureAwait(false);

        if (lockToken is null)
        {
            // Another caller is repopulating; brief wait + recheck cache.
            await Task.Delay(StampedeWait, cancellationToken).ConfigureAwait(false);
            cached = await _cache.TryGetAsync(request.MerchantId, request.Date, cancellationToken)
                .ConfigureAwait(false);
            if (cached is not null)
                return BuildDto(cached, hit: true);

            _logger.LogDebug(
                "Stampede lock missed cache repopulate window for {MerchantId} {Date} — falling through to Mongo",
                request.MerchantId, request.Date);
        }

        try
        {
            var projection = await _repository
                .GetAsync(request.MerchantId, request.Date, cancellationToken)
                .ConfigureAwait(false);

            if (projection is null)
            {
                // No entries yet for the day. Return empty payload but DON'T cache it —
                // a fresh event in seconds would otherwise hit stale "zero" for 60s.
                return DailyBalanceDto.Empty(request.MerchantId, request.Date, _clock.UtcNow, new CacheInfo(false, 0));
            }

            await _cache.SetAsync(projection, cancellationToken).ConfigureAwait(false);
            return BuildDto(projection, hit: false);
        }
        finally
        {
            if (lockToken is not null)
            {
                await _cache.ReleaseStampedeLockAsync(
                    request.MerchantId, request.Date, lockToken, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private DailyBalanceDto BuildDto(Domain.Projections.DailyBalanceReadModel m, bool hit)
    {
        var age = hit
            ? Math.Max(0, (int)Math.Round((_clock.UtcNow - m.LastUpdatedAt).TotalSeconds))
            : 0;
        return DailyBalanceDto.FromReadModel(m, new CacheInfo(hit, age));
    }
}
