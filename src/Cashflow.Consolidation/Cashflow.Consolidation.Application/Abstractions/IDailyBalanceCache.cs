using Cashflow.Consolidation.Domain.Projections;

namespace Cashflow.Consolidation.Application.Abstractions;

public interface IDailyBalanceCache
{
    Task<DailyBalanceReadModel?> TryGetAsync(
        Guid merchantId,
        DateOnly date,
        CancellationToken cancellationToken);

    Task SetAsync(
        DailyBalanceReadModel value,
        CancellationToken cancellationToken);

    Task<string?> TryAcquireStampedeLockAsync(
        Guid merchantId,
        DateOnly date,
        CancellationToken cancellationToken);

    Task ReleaseStampedeLockAsync(
        Guid merchantId,
        DateOnly date,
        string token,
        CancellationToken cancellationToken);
}
