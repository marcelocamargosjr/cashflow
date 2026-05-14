using Cashflow.Consolidation.Application.Abstractions;
using Cashflow.Consolidation.Domain.Projections;
using Cashflow.Consolidation.Infrastructure.Persistence.Documents;
using MongoDB.Driver;

namespace Cashflow.Consolidation.Infrastructure.Persistence;

public sealed class DailyBalanceReadRepository : IDailyBalanceReadRepository
{
    private readonly MongoContext _context;

    public DailyBalanceReadRepository(MongoContext context)
    {
        _context = context;
    }

    public async Task<DailyBalanceReadModel?> GetAsync(
        Guid merchantId,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var id = DailyBalanceDoc.BuildId(merchantId, date);
        var doc = await _context.DailyBalances
            .Find(d => d.Id == id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return doc?.ToReadModel();
    }

    public async Task<IReadOnlyList<DailyBalanceReadModel>> GetRangeAsync(
        Guid merchantId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var fromUtc = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtc = to.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var filter = Builders<DailyBalanceDoc>.Filter.And(
            Builders<DailyBalanceDoc>.Filter.Eq(d => d.MerchantId, merchantId),
            Builders<DailyBalanceDoc>.Filter.Gte(d => d.Date, fromUtc),
            Builders<DailyBalanceDoc>.Filter.Lte(d => d.Date, toUtc));

        var docs = await _context.DailyBalances
            .Find(filter)
            .SortBy(d => d.Date)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return docs.ConvertAll(d => d.ToReadModel());
    }
}
