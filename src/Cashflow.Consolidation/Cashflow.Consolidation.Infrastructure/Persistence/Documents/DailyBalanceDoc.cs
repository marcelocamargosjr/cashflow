using Cashflow.Consolidation.Domain.Projections;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Cashflow.Consolidation.Infrastructure.Persistence.Documents;

public sealed class DailyBalanceDoc
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("merchantId")]
    [BsonRepresentation(BsonType.String)]
    public Guid MerchantId { get; set; }

    [BsonElement("date")]
    public DateTime Date { get; set; }

    [BsonElement("totalCredits")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal TotalCredits { get; set; }

    [BsonElement("totalDebits")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal TotalDebits { get; set; }

    [BsonElement("entriesCount")]
    public int EntriesCount { get; set; }

    [BsonElement("byCategory")]
    public List<CategoryBucketDoc> ByCategory { get; set; } = new();

    [BsonElement("lastUpdatedAt")]
    public DateTime LastUpdatedAt { get; set; }

    [BsonElement("revision")]
    public long Revision { get; set; }

    [BsonElement("lastAppliedEventId")]
    [BsonIgnoreIfNull]
    [BsonRepresentation(BsonType.String)]
    public Guid? LastAppliedEventId { get; set; }

    public DailyBalanceReadModel ToReadModel()
    {
        var totalCredits = TotalCredits;
        var totalDebits = TotalDebits;
        var balance = totalCredits - totalDebits;

        var buckets = ByCategory
            .Select(b => new CategoryBucket(b.Category, b.Credit, b.Debit, b.Count))
            .ToList();

        return new DailyBalanceReadModel(
            MerchantId,
            DateOnly.FromDateTime(Date),
            totalCredits,
            totalDebits,
            balance,
            EntriesCount,
            buckets,
            new DateTimeOffset(DateTime.SpecifyKind(LastUpdatedAt, DateTimeKind.Utc)),
            Revision,
            LastAppliedEventId);
    }

    public static string BuildId(Guid merchantId, DateOnly date) =>
        $"{merchantId:D}:{date:yyyy-MM-dd}";
}
