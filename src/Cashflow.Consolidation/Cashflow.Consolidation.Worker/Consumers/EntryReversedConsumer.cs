using Cashflow.Consolidation.Infrastructure.Persistence;
using Cashflow.Consolidation.Infrastructure.Persistence.Documents;
using Cashflow.Consolidation.Infrastructure.Projections;
using Cashflow.Contracts.V1;
using Cashflow.SharedKernel.Observability;
using Cashflow.SharedKernel.Time;
using MassTransit;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Cashflow.Consolidation.Worker.Consumers;

/// <summary>
/// Apply-first consumer for <see cref="EntryReversedV1"/>. Same orchestration as
/// <see cref="EntryRegisteredConsumer"/> — the projector handles sign via the snapshot.
/// </summary>
public sealed class EntryReversedConsumer : IConsumer<EntryReversedV1>
{
    private readonly MongoContext _mongo;
    private readonly IProjectionService _projection;
    private readonly IClock _clock;
    private readonly ILogger<EntryReversedConsumer> _logger;

    public EntryReversedConsumer(
        MongoContext mongo,
        IProjectionService projection,
        IClock clock,
        ILogger<EntryReversedConsumer> logger)
    {
        _mongo = mongo;
        _projection = projection;
        _clock = clock;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<EntryReversedV1> context)
    {
        var evt = context.Message;
        var ct = context.CancellationToken;

        var lag = (_clock.UtcNow - evt.OccurredOn).TotalSeconds;
        if (lag >= 0)
            CashflowMeters.ProjectionLagSeconds.Record(
                lag,
                new KeyValuePair<string, object?>("event", "EntryReversedV1"));

        var seen = await _mongo.ProcessedEvents
            .Find(x => x.Id == evt.EventId)
            .Project(x => x.Id)
            .AnyAsync(ct)
            .ConfigureAwait(false);

        if (seen)
        {
            _logger.LogInformation(
                "EntryReversedV1 {EventId} skipped — fast-path hit (already processed)",
                evt.EventId);
            return;
        }

        var applied = await _projection.ApplyReversalAsync(evt, ct).ConfigureAwait(false);

        try
        {
            await _mongo.ProcessedEvents.InsertOneAsync(
                new ProcessedEventDoc { Id = evt.EventId, ProcessedAt = _clock.UtcNow.UtcDateTime },
                cancellationToken: ct).ConfigureAwait(false);
        }
        catch (MongoWriteException ex)
            when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
        }

        if (!applied)
        {
            _logger.LogInformation(
                "EntryReversedV1 {EventId} was already applied to projection — guard blocked",
                evt.EventId);
        }
    }
}
