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

        // Lag observado pelo consumer = wall clock no início do Consume - momento em que o evento foi gerado.
        // Inclui tempo de outbox + broker + filas. Histograma em segundos.
        var lag = (_clock.UtcNow - evt.OccurredOn).TotalSeconds;
        if (lag >= 0)
            CashflowMeters.ProjectionLagSeconds.Record(
                lag,
                new KeyValuePair<string, object?>("event", "EntryRegisteredV1"));

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
