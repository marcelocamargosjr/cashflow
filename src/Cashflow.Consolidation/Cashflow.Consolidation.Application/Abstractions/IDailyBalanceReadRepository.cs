using Cashflow.Consolidation.Domain.Projections;

namespace Cashflow.Consolidation.Application.Abstractions;

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
