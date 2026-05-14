namespace Cashflow.Contracts.V1;

/// <summary>
/// Base contract for every integration event published from one BC to another.
/// EventId guarantees idempotency at the consumer. OccurredOn is the domain instant.
/// Version is redundant with the namespace but explicit routing is easier this way.
/// Evolution rules:
///   - Add optional field      → keep same version (V1).
///   - Add required field      → new version (V2).
///   - Remove/rename field     → new version (V2) + adapter in consumer.
///   - Change semantics        → ALWAYS new version.
/// </summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredOn { get; }
    int Version { get; }
}
