using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Cashflow.Consolidation.Infrastructure.Persistence.Documents;

public sealed record ProcessedEventDoc
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; init; }

    [BsonElement("processedAt")]
    public DateTime ProcessedAt { get; init; }
}
