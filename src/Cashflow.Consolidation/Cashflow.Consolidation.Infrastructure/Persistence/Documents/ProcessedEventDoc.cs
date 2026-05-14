using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Cashflow.Consolidation.Infrastructure.Persistence.Documents;

/// <summary>
/// Fast-path idempotency marker. Correctness comes from the atomic
/// <c>lastAppliedEventId</c> guard on <c>daily_balances</c> — see <c>05-DADOS.md §2.5</c>.
/// </summary>
public sealed record ProcessedEventDoc
{
    /// <summary>
    /// The Integration Event UUID. <c>[BsonRepresentation(BsonType.String)]</c> is
    /// MANDATORY — the default driver serialization for Guid is BinData(3|4), which
    /// would mismatch the `_id: "evt-uuid"` schema declared in <c>05-DADOS.md §2.4</c>
    /// and break the duplicate-key idempotency check.
    /// </summary>
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; init; }

    [BsonElement("processedAt")]
    public DateTime ProcessedAt { get; init; }
}
