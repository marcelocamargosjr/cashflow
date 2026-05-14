using Cashflow.Consolidation.Domain.Projections;

namespace Cashflow.Consolidation.Application.Abstractions;

/// <summary>
/// Read access to the persisted projection (Mongo in production).
/// </summary>
public interface IDailyBalanceReadRepository
{
    Task<DailyBalanceReadModel?> GetAsync(
        Guid merchantId,
        DateOnly date,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DailyBalanceReadModel>> GetRangeAsync(
        Guid merchantId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);
}
