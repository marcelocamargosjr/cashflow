using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Cashflow.Consolidation.Infrastructure.Persistence.Documents;

public sealed class CategoryBucketDoc
{
    [BsonElement("category")]
    public string Category { get; set; } = string.Empty;

    [BsonElement("credit")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal Credit { get; set; }

    [BsonElement("debit")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal Debit { get; set; }

    [BsonElement("count")]
    public int Count { get; set; }
}
