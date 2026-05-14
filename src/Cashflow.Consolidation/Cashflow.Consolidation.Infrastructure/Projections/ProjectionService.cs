using Cashflow.Consolidation.Infrastructure.Persistence;
using Cashflow.Consolidation.Infrastructure.Persistence.Documents;
using Cashflow.Contracts.V1;
using Cashflow.SharedKernel.Time;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Cashflow.Consolidation.Infrastructure.Projections;

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
        var deltas = BuildDeltas(type, amount, sign);
        var ctx = BuildContext(eventId, merchantId, entryDate, category, deltas);

        var pass1 = await _context.DailyBalances
            .UpdateOneAsync(ctx.Pass1Filter, ctx.Pass1Update, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (pass1.ModifiedCount > 0)
            return true;

        var options = new UpdateOptions { IsUpsert = isUpsertAllowed };

        try
        {
            var pass2 = await _context.DailyBalances
                .UpdateOneAsync(ctx.Pass2Filter, ctx.Pass2Update, options, cancellationToken)
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
            return await RetryAfterDuplicateKeyAsync(ctx, eventId, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<bool> RetryAfterDuplicateKeyAsync(
        UpdateContext ctx,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        // Upsert raced against another consumer for the same `_id`. The doc now
        // exists; our event is NOT yet applied. Retry Pass 1 (existing bucket) and
        // then Pass 2 without upsert. The guard ensures we never apply twice.
        _logger.LogInformation(
            "Event {EventId} for {Id} hit DuplicateKey on upsert — retrying as non-upsert update",
            eventId, ctx.DocId);

        var retryPass1 = await _context.DailyBalances
            .UpdateOneAsync(ctx.Pass1Filter, ctx.Pass1Update, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (retryPass1.ModifiedCount > 0)
            return true;

        var retryOptions = new UpdateOptions { IsUpsert = false };
        var retryPass2 = await _context.DailyBalances
            .UpdateOneAsync(ctx.Pass2Filter, ctx.Pass2Update, retryOptions, cancellationToken)
            .ConfigureAwait(false);
        if (retryPass2.ModifiedCount > 0)
            return true;

        _logger.LogWarning(
            "Event {EventId} for {Id} could not be applied after DuplicateKey retry — guard most likely matched",
            eventId, ctx.DocId);
        return false;
    }

    private static Deltas BuildDeltas(string type, decimal amount, int sign)
    {
        var isCredit = string.Equals(type, "Credit", StringComparison.OrdinalIgnoreCase);
        return new Deltas(
            Credit: isCredit ? sign * amount : 0m,
            Debit: isCredit ? 0m : sign * amount,
            // +1 on register, -1 on reverse — count snapshots the entries count.
            Count: sign);
    }

    private UpdateContext BuildContext(
        Guid eventId,
        Guid merchantId,
        DateOnly entryDate,
        string? category,
        Deltas deltas)
    {
        var docId = DailyBalanceDoc.BuildId(merchantId, entryDate);
        var bucketKey = string.IsNullOrWhiteSpace(category) ? UncategorizedBucket : category;
        var now = _clock.UtcNow.UtcDateTime;
        var filterBuilder = Builders<DailyBalanceDoc>.Filter;
        var updateBuilder = Builders<DailyBalanceDoc>.Update;

        // Guard: never apply the same event twice.
        var guard = filterBuilder.Or(
            filterBuilder.Exists(d => d.LastAppliedEventId, false),
            filterBuilder.Ne(d => d.LastAppliedEventId, eventId));

        var pass1Filter = filterBuilder.And(
            filterBuilder.Eq(d => d.Id, docId),
            guard,
            filterBuilder.ElemMatch(d => d.ByCategory, b => b.Category == bucketKey));

        var pass1Update = updateBuilder.Combine(
            updateBuilder.Inc(d => d.TotalCredits, deltas.Credit),
            updateBuilder.Inc(d => d.TotalDebits, deltas.Debit),
            updateBuilder.Inc(d => d.EntriesCount, deltas.Count),
            updateBuilder.Inc(d => d.Revision, 1L),
            updateBuilder.Set(d => d.LastUpdatedAt, now),
            updateBuilder.Set(d => d.LastAppliedEventId, eventId),
            // Positional `$` operator targets the matched bucket from `ElemMatch`.
            updateBuilder.Inc("byCategory.$.credit", new Decimal128(deltas.Credit)),
            updateBuilder.Inc("byCategory.$.debit", new Decimal128(deltas.Debit)),
            updateBuilder.Inc("byCategory.$.count", deltas.Count));

        var pass2Filter = filterBuilder.And(
            filterBuilder.Eq(d => d.Id, docId),
            guard);

        var newBucket = new CategoryBucketDoc
        {
            Category = bucketKey,
            Credit = deltas.Credit,
            Debit = deltas.Debit,
            Count = deltas.Count
        };

        var dateUtcMidnight = entryDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var pass2Update = updateBuilder.Combine(
            updateBuilder.Inc(d => d.TotalCredits, deltas.Credit),
            updateBuilder.Inc(d => d.TotalDebits, deltas.Debit),
            updateBuilder.Inc(d => d.EntriesCount, deltas.Count),
            updateBuilder.Inc(d => d.Revision, 1L),
            updateBuilder.Set(d => d.LastUpdatedAt, now),
            updateBuilder.Set(d => d.LastAppliedEventId, eventId),
            updateBuilder.Push(d => d.ByCategory, newBucket),
            updateBuilder.SetOnInsert(d => d.MerchantId, merchantId),
            updateBuilder.SetOnInsert(d => d.Date, dateUtcMidnight));

        return new UpdateContext(docId, pass1Filter, pass1Update, pass2Filter, pass2Update);
    }

    private readonly record struct Deltas(decimal Credit, decimal Debit, int Count);

    private sealed record UpdateContext(
        string DocId,
        FilterDefinition<DailyBalanceDoc> Pass1Filter,
        UpdateDefinition<DailyBalanceDoc> Pass1Update,
        FilterDefinition<DailyBalanceDoc> Pass2Filter,
        UpdateDefinition<DailyBalanceDoc> Pass2Update);
}
