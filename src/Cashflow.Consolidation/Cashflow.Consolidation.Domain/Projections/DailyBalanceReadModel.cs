namespace Cashflow.Consolidation.Domain.Projections;

public sealed record DailyBalanceReadModel(
    Guid MerchantId,
    DateOnly Date,
    decimal TotalCredits,
    decimal TotalDebits,
    decimal Balance,
    int EntriesCount,
    IReadOnlyList<CategoryBucket> ByCategory,
    DateTimeOffset LastUpdatedAt,
    long Revision,
    Guid? LastAppliedEventId);
