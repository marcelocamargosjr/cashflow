using Cashflow.Consolidation.Domain.Projections;

namespace Cashflow.Consolidation.Application.Balances.Dtos;

public sealed record DailyBalanceDto(
    Guid MerchantId,
    DateOnly Date,
    decimal TotalCredits,
    decimal TotalDebits,
    decimal Balance,
    int EntriesCount,
    IReadOnlyList<CategoryBucketDto> ByCategory,
    DateTimeOffset LastUpdatedAt,
    long Revision,
    CacheInfo Cache)
{
    public static DailyBalanceDto FromReadModel(DailyBalanceReadModel m, CacheInfo cache) =>
        new(
            m.MerchantId,
            m.Date,
            m.TotalCredits,
            m.TotalDebits,
            m.Balance,
            m.EntriesCount,
            m.ByCategory
                .Select(b => new CategoryBucketDto(b.Category, b.Credit, b.Debit, b.Count))
                .ToList(),
            m.LastUpdatedAt,
            m.Revision,
            cache);

    public static DailyBalanceDto Empty(Guid merchantId, DateOnly date, DateTimeOffset now, CacheInfo cache) =>
        new(merchantId, date, 0m, 0m, 0m, 0, Array.Empty<CategoryBucketDto>(), now, 0L, cache);
}

public sealed record CacheInfo(bool Hit, int AgeSeconds);
