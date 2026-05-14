using Cashflow.Consolidation.Domain.Projections;

namespace Cashflow.Consolidation.Application.Abstractions;

/// <summary>
/// Cache-aside contract for the daily balance projection. Implementations MUST
/// honour TTL, stampede protection, and graceful degradation as described in
/// <c>05-DADOS.md §3.3</c>.
/// </summary>
public interface IDailyBalanceCache
{
    /// <summary>
    /// Returns the cached value or <c>null</c> on a miss. Never throws.
    /// </summary>
    Task<DailyBalanceReadModel?> TryGetAsync(
        Guid merchantId,
        DateOnly date,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stores the read model in the cache with the configured TTL.
    /// </summary>
    Task SetAsync(
        DailyBalanceReadModel value,
        CancellationToken cancellationToken);

    /// <summary>
    /// Acquires the stampede lock (SET NX EX). Returns the lock token to be passed to
    /// <see cref="ReleaseStampedeLockAsync"/>, or <c>null</c> if the lock is taken.
    /// </summary>
    Task<string?> TryAcquireStampedeLockAsync(
        Guid merchantId,
        DateOnly date,
        CancellationToken cancellationToken);

    /// <summary>
    /// Releases the stampede lock — atomic compare-and-delete via Lua. Never throws.
    /// </summary>
    Task ReleaseStampedeLockAsync(
        Guid merchantId,
        DateOnly date,
        string token,
        CancellationToken cancellationToken);
}
