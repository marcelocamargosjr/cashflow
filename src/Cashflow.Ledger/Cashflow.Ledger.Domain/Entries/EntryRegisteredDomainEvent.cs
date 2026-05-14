using Cashflow.SharedKernel.Domain;

namespace Cashflow.Ledger.Domain.Entries;

public sealed record EntryRegisteredDomainEvent(Entry Entry) : IDomainEvent
{
    public Guid EventId { get; } = Guid.CreateVersion7();
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}
