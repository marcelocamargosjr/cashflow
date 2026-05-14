using Cashflow.Ledger.Domain.Abstractions;
using Cashflow.Ledger.Domain.Entries;
using Cashflow.Ledger.Infrastructure.Messaging;
using Cashflow.SharedKernel.Domain;
using Cashflow.SharedKernel.Time;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cashflow.Ledger.Infrastructure.Persistence;

public sealed class LedgerDbContext : DbContext, IUnitOfWork
{
    // Lazy: o BusOutbox do MassTransit substitui o IPublishEndpoint por um wrapper
    // que depende de IServiceProvider para localizar o DbContext em runtime.
    // Resolver IPublishEndpoint no construtor cria um ciclo de dependência durante
    // a construção do escopo (DI deadlock); resolver sob demanda quebra o ciclo.
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;

    public LedgerDbContext(
        DbContextOptions<LedgerDbContext> options,
        IServiceProvider serviceProvider,
        IClock clock)
        : base(options)
    {
        _serviceProvider = serviceProvider;
        _clock = clock;
    }

    public DbSet<Entry> Entries => Set<Entry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("ledger");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LedgerDbContext).Assembly);

        // MassTransit Outbox (producer-only — no InboxState; idempotência do consumer no Mongo via ADR-0007).
        modelBuilder.AddOutboxMessageEntity(c => c.ToTable("OutboxMessage", "messaging"));
        modelBuilder.AddOutboxStateEntity(c => c.ToTable("OutboxState", "messaging"));
        // AddOutboxMessageEntity declara HasOne<InboxState> internamente — remove para o Ledger,
        // que é producer-only. Ver ADR-0007.
        modelBuilder.Ignore<InboxState>();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var aggregates = ChangeTracker.Entries<AggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        var domainEvents = aggregates.SelectMany(a => a.DomainEvents).ToList();

        if (domainEvents.Count > 0)
        {
            var publishEndpoint = _serviceProvider.GetRequiredService<IPublishEndpoint>();
            foreach (var de in domainEvents)
            {
                var integrationEvent = IntegrationEventMapper.Map(de, _clock.UtcNow);
                if (integrationEvent is not null)
                {
                    await publishEndpoint
                        .Publish(integrationEvent, integrationEvent.GetType(), cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }

        var result = await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // ATENÇÃO (patch C2): limpar APÓS o commit. Com NpgsqlRetryingExecutionStrategy,
        // SaveChangesAsync pode ser re-executado pelo EF; limpar antes apagaria os events
        // do retry e o publish nunca ocorreria. Ver IT-09.
        foreach (var aggregate in aggregates)
            aggregate.ClearDomainEvents();

        return result;
    }
}
