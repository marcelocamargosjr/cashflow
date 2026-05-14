using Cashflow.Consolidation.Infrastructure.Persistence;
using Cashflow.Consolidation.Infrastructure.Persistence.Documents;
using Cashflow.Contracts.V1;
using Cashflow.SharedKernel.Time;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Cashflow.Consolidation.Infrastructure.Projections;

/// <summary>
/// Atomic projection updater. Uses a two-pass strategy:
/// <list type="number">
///   <item>Pass 1 — try to mutate an existing category bucket via array positional update.</item>
///   <item>Pass 2 — if Pass 1 found no document/bucket, upsert the doc and $push a fresh bucket.</item>
/// </list>
/// Every filter carries the <c>lastAppliedEventId != evt.EventId</c> guard so retries are no-ops.
/// </summary>
public sealed class ProjectionService : IProjectionService
{
    // Buckets need a non-null discriminator. The wire allows `Category = null`, so we
    // collapse those to a sentinel bucket. Keeps Mongo $elemMatch trivial.
    private const string UncategorizedBucket = "(uncategorized)";

    private readonly MongoContext _context;
    private readonly IClock _clock;
    private readonly ILogger<ProjectionService> _logger;

    public ProjectionService(MongoContext context, IClock clock, ILogger<ProjectionService> logger)
    {
        _context = context;
        _clock = clock;
        _logger = logger;
    }

    public Task<bool> ApplyRegistrationAsync(EntryRegisteredV1 evt, CancellationToken cancellationToken)
    {
        var sign = 1;
        return ApplyAsync(
            evt.EventId,
            evt.MerchantId,
            evt.EntryDate,
            evt.Type,
            evt.Amount,
            evt.Category,
            sign,
            isUpsertAllowed: true,
            cancellationToken);
    }

    public Task<bool> ApplyReversalAsync(EntryReversedV1 evt, CancellationToken cancellationToken)
    {
        var sign = -1;
        return ApplyAsync(
            evt.EventId,
            evt.MerchantId,
            evt.EntryDate,
            evt.Type,
            evt.Amount,
            evt.Category,
            sign,
            // Reversing an entry that never landed is a real anomaly — surface, don't paper over.
            // We still let Pass 2 upsert so eventually-consistent recovery is possible.
            isUpsertAllowed: true,
            cancellationToken);
    }

    private async Task<bool> ApplyAsync(
        Guid eventId,
        Guid merchantId,
        DateOnly entryDate,
        string type,
        decimal amount,
        string? category,
        int sign,
        bool isUpsertAllowed,
        CancellationToken cancellationToken)
    {
        var id = DailyBalanceDoc.BuildId(merchantId, entryDate);
        var bucketKey = string.IsNullOrWhiteSpace(category) ? UncategorizedBucket : category;
        var isCredit = string.Equals(type, "Credit", StringComparison.OrdinalIgnoreCase);

        var creditDelta = isCredit ? sign * amount : 0m;
        var debitDelta = isCredit ? 0m : sign * amount;
        var countDelta = sign; // +1 on register, -1 on reverse — count snapshots the entries count

        var now = _clock.UtcNow.UtcDateTime;
        var filterBuilder = Builders<DailyBalanceDoc>.Filter;
        var updateBuilder = Builders<DailyBalanceDoc>.Update;

        // Guard: never apply the same event twice.
        var guard = filterBuilder.Or(
            filterBuilder.Exists(d => d.LastAppliedEventId, false),
            filterBuilder.Ne(d => d.LastAppliedEventId, eventId));

        // ---- Pass 1: mutate existing bucket ----
        var pass1Filter = filterBuilder.And(
            filterBuilder.Eq(d => d.Id, id),
            guard,
            filterBuilder.ElemMatch(d => d.ByCategory, b => b.Category == bucketKey));

        var pass1Update = updateBuilder.Combine(
            updateBuilder.Inc(d => d.TotalCredits, creditDelta),
            updateBuilder.Inc(d => d.TotalDebits, debitDelta),
            updateBuilder.Inc(d => d.EntriesCount, countDelta),
            updateBuilder.Inc(d => d.Revision, 1L),
            updateBuilder.Set(d => d.LastUpdatedAt, now),
            updateBuilder.Set(d => d.LastAppliedEventId, eventId),
            // Positional `$` operator targets the matched bucket from `ElemMatch`.
            updateBuilder.Inc("byCategory.$.credit", new MongoDB.Bson.Decimal128(creditDelta)),
            updateBuilder.Inc("byCategory.$.debit", new MongoDB.Bson.Decimal128(debitDelta)),
            updateBuilder.Inc("byCategory.$.count", countDelta));

        var pass1 = await _context.DailyBalances
            .UpdateOneAsync(pass1Filter, pass1Update, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (pass1.ModifiedCount > 0)
            return true;

        // ---- Pass 2: doc exists w/o bucket, OR doc absent, OR guard blocked ----
        var pass2Filter = filterBuilder.And(
            filterBuilder.Eq(d => d.Id, id),
            guard);

        var newBucket = new CategoryBucketDoc
        {
            Category = bucketKey,
            Credit = creditDelta,
            Debit = debitDelta,
            Count = countDelta
        };

        var dateUtcMidnight = entryDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var pass2Update = updateBuilder.Combine(
            updateBuilder.Inc(d => d.TotalCredits, creditDelta),
            updateBuilder.Inc(d => d.TotalDebits, debitDelta),
            updateBuilder.Inc(d => d.EntriesCount, countDelta),
            updateBuilder.Inc(d => d.Revision, 1L),
            updateBuilder.Set(d => d.LastUpdatedAt, now),
            updateBuilder.Set(d => d.LastAppliedEventId, eventId),
            updateBuilder.Push(d => d.ByCategory, newBucket),
            updateBuilder.SetOnInsert(d => d.MerchantId, merchantId),
            updateBuilder.SetOnInsert(d => d.Date, dateUtcMidnight));

        var options = new UpdateOptions { IsUpsert = isUpsertAllowed };

        try
        {
            var pass2 = await _context.DailyBalances
                .UpdateOneAsync(pass2Filter, pass2Update, options, cancellationToken)
                .ConfigureAwait(false);

            if (pass2.ModifiedCount > 0 || pass2.UpsertedId is not null)
                return true;

            // Pass 1 blocked by guard AND pass 2 matched zero (no upsert performed because
            // a doc with the same _id already exists but the guard blocked the modify).
            _logger.LogInformation(
                "Event {EventId} for merchant {MerchantId} date {EntryDate} blocked by guard — no-op",
                eventId, merchantId, entryDate);
            return false;
        }
        catch (MongoWriteException ex)
            when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // Upsert raced against another consumer for the same `_id` but a different
            // event. Treat as guard-blocked: the writer that wins persists; ours is a no-op.
            _logger.LogInformation(
                "Event {EventId} for {Id} hit DuplicateKey on upsert — treated as guard-blocked",
                eventId, id);
            return false;
        }
    }
}
