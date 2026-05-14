using Cashflow.Consolidation.Infrastructure.Persistence;
using Cashflow.Consolidation.Infrastructure.Persistence.Documents;
using MongoDB.Driver;

namespace Cashflow.Consolidation.IntegrationTests.Infrastructure;

public static class ProjectionWaiter
{
    public static async Task<DailyBalanceDoc> WaitForAsync(
        MongoContext mongo,
        Guid merchantId,
        DateOnly date,
        Func<DailyBalanceDoc, bool> predicate,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null,
        CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
        var step = pollInterval ?? TimeSpan.FromMilliseconds(150);
        var id = DailyBalanceDoc.BuildId(merchantId, date);

        while (DateTime.UtcNow < deadline)
        {
            var doc = await mongo.DailyBalances
                .Find(d => d.Id == id)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            if (doc is not null && predicate(doc))
                return doc;

            await Task.Delay(step, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException(
            $"Projection for merchant={merchantId} date={date:yyyy-MM-dd} did not satisfy predicate within {timeout}.");
    }

    public static async Task<long> CountProcessedEventsAsync(
        MongoContext mongo,
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        return await mongo.ProcessedEvents
            .CountDocumentsAsync(
                Builders<ProcessedEventDoc>.Filter.Eq(d => d.Id, eventId),
                cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
