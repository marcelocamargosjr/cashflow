using System.Text.Json;
using Cashflow.Consolidation.Application.Abstractions;
using Cashflow.Consolidation.Domain.Projections;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Cashflow.Consolidation.Infrastructure.Caching;

/// <summary>
/// Redis-backed cache implementing the cache-aside contract from
/// <c>05-DADOS.md §3.3</c>. Logs errors and degrades gracefully — never throws to the caller.
/// </summary>
public sealed class RedisDailyBalanceCache : IDailyBalanceCache
{
    // Atomic "release lock if we still own the token". Avoids the foot-gun where a
    // delayed release deletes someone else's lock acquired after our TTL expired.
    private const string ReleaseLockScript = """
        if redis.call("GET", KEYS[1]) == ARGV[1] then
            return redis.call("DEL", KEYS[1])
        else
            return 0
        end
        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly IConnectionMultiplexer _multiplexer;
    private readonly RedisOptions _options;
    private readonly ILogger<RedisDailyBalanceCache> _logger;

    public RedisDailyBalanceCache(
        IConnectionMultiplexer multiplexer,
        IOptions<RedisOptions> options,
        ILogger<RedisDailyBalanceCache> logger)
    {
        _multiplexer = multiplexer;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<DailyBalanceReadModel?> TryGetAsync(
        Guid merchantId,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        try
        {
            var db = _multiplexer.GetDatabase();
            var value = await db.StringGetAsync(CacheKey(merchantId, date)).ConfigureAwait(false);
            if (value.IsNullOrEmpty)
                return null;

            return JsonSerializer.Deserialize<CachedBalance>(value!, JsonOptions)?.ToReadModel(merchantId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache read failed for {MerchantId} {Date}", merchantId, date);
            return null;
        }
    }

    public async Task SetAsync(DailyBalanceReadModel value, CancellationToken cancellationToken)
    {
        try
        {
            var db = _multiplexer.GetDatabase();
            var payload = JsonSerializer.Serialize(CachedBalance.FromReadModel(value), JsonOptions);
            await db.StringSetAsync(
                CacheKey(value.MerchantId, value.Date),
                payload,
                TimeSpan.FromSeconds(_options.DailyTtlSeconds)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache write failed for {MerchantId} {Date}", value.MerchantId, value.Date);
        }
    }

    public async Task<string?> TryAcquireStampedeLockAsync(
        Guid merchantId,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        try
        {
            var db = _multiplexer.GetDatabase();
            var token = Guid.NewGuid().ToString("N");
            var acquired = await db.StringSetAsync(
                LockKey(merchantId, date),
                token,
                TimeSpan.FromSeconds(_options.StampedeLockTtlSeconds),
                when: When.NotExists).ConfigureAwait(false);

            return acquired ? token : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis stampede lock acquire failed for {MerchantId} {Date}", merchantId, date);
            // On Redis outage, fall through unguarded — graceful degradation.
            return null;
        }
    }

    public async Task ReleaseStampedeLockAsync(
        Guid merchantId,
        DateOnly date,
        string token,
        CancellationToken cancellationToken)
    {
        try
        {
            var db = _multiplexer.GetDatabase();
            await db.ScriptEvaluateAsync(
                ReleaseLockScript,
                new RedisKey[] { LockKey(merchantId, date) },
                new RedisValue[] { token }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis stampede lock release failed for {MerchantId} {Date}", merchantId, date);
        }
    }

    private static string CacheKey(Guid merchantId, DateOnly date) =>
        $"balance:daily:{merchantId:D}:{date:yyyyMMdd}";

    private static string LockKey(Guid merchantId, DateOnly date) =>
        $"lock:balance:daily:{merchantId:D}:{date:yyyyMMdd}";

    private sealed record CachedBalance(
        DateOnly Date,
        decimal TotalCredits,
        decimal TotalDebits,
        int EntriesCount,
        IReadOnlyList<CategoryBucket> ByCategory,
        DateTimeOffset LastUpdatedAt,
        long Revision,
        Guid? LastAppliedEventId)
    {
        public DailyBalanceReadModel ToReadModel(Guid merchantId) =>
            new(merchantId, Date, TotalCredits, TotalDebits, TotalCredits - TotalDebits,
                EntriesCount, ByCategory, LastUpdatedAt, Revision, LastAppliedEventId);

        public static CachedBalance FromReadModel(DailyBalanceReadModel m) =>
            new(m.Date, m.TotalCredits, m.TotalDebits, m.EntriesCount, m.ByCategory,
                m.LastUpdatedAt, m.Revision, m.LastAppliedEventId);
    }
}
