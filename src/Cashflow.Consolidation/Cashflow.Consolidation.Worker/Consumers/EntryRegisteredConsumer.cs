using Cashflow.Consolidation.Infrastructure.Persistence;
using Cashflow.Consolidation.Infrastructure.Persistence.Documents;
using Cashflow.Consolidation.Infrastructure.Projections;
using Cashflow.Contracts.V1;
using Cashflow.SharedKernel.Time;
using MassTransit;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Cashflow.Consolidation.Worker.Consumers;

/// <summary>
/// Apply-first consumer for <see cref="EntryRegisteredV1"/>.
///
/// Order (patch C1 in `14-PATCHES-CIRURGICOS.md`):
/// <list type="number">
///   <item>Fast-path: skip if <c>processed_events</c> already contains this <c>EventId</c>.</item>
///   <item>Apply the projection atomically with the <c>lastAppliedEventId</c> guard.</item>
///   <item>Mark the event as processed.</item>
/// </list>
/// Correctness comes from step 2 (guard), NOT from step 1. If the broker redelivers
/// after step 2 completed but step 3 failed, the next attempt re-runs step 2 — the
/// guard blocks (ModifiedCount = 0) and the no-op marks step 3 again.
/// </summary>
public sealed class EntryRegisteredConsumer : IConsumer<EntryRegisteredV1>
{
    private readonly MongoContext _mongo;
    private readonly IProjectionService _projection;
    private readonly IClock _clock;
    private readonly ILogger<EntryRegisteredConsumer> _logger;

    public EntryRegisteredConsumer(
        MongoContext mongo,
        IProjectionService projection,
        IClock clock,
        ILogger<EntryRegisteredConsumer> logger)
    {
        _mongo = mongo;
        _projection = projection;
        _clock = clock;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<EntryRegisteredV1> context)
    {
        var evt = context.Message;
        var ct = context.CancellationToken;

        var seen = await _mongo.ProcessedEvents
            .Find(x => x.Id == evt.EventId)
            .Project(x => x.Id)
            .AnyAsync(ct)
            .ConfigureAwait(false);

        if (seen)
        {
            _logger.LogInformation(
                "EntryRegisteredV1 {EventId} skipped — fast-path hit (already processed)",
                evt.EventId);
            return;
        }

        var applied = await _projection.ApplyRegistrationAsync(evt, ct).ConfigureAwait(false);

        try
        {
            await _mongo.ProcessedEvents.InsertOneAsync(
                new ProcessedEventDoc { Id = evt.EventId, ProcessedAt = _clock.UtcNow.UtcDateTime },
                cancellationToken: ct).ConfigureAwait(false);
        }
        catch (MongoWriteException ex)
            when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // Concurrent retry got there first. Safe to ack.
        }

        if (!applied)
        {
            _logger.LogInformation(
                "EntryRegisteredV1 {EventId} was already applied to projection — guard blocked",
                evt.EventId);
        }
    }
}
