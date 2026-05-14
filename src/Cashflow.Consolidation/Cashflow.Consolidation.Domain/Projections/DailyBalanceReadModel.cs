namespace Cashflow.Consolidation.Domain.Projections;

/// <summary>
/// Read model of the daily consolidated balance for a merchant.
/// Source of truth is the Mongo document; this record is the cross-layer
/// representation used by Application/Api. See <c>04-DOMINIO-E-API.md §1.2</c>
/// for the schema and <c>05-DADOS.md §2.3</c> for the persisted form.
/// </summary>
/// <param name="Revision">Monotonic counter of events applied to the projection.
/// NOT optimistic concurrency — used for observability and reprojection.</param>
/// <param name="LastAppliedEventId">Guard for the atomic <c>FindOneAndUpdate</c>
/// that prevents double-application even when <c>processed_events</c> fails to mark
/// (patch C1 in <c>14-PATCHES-CIRURGICOS.md</c>). <c>null</c> means the projection
/// has never been touched by any event (placeholder doc).</param>
public sealed record DailyBalanceReadModel(
    Guid MerchantId,
    DateOnly Date,
    decimal TotalCredits,
    decimal TotalDebits,
    decimal Balance,
    int EntriesCount,
    IReadOnlyList<CategoryBucket> ByCategory,
    DateTimeOffset LastUpdatedAt,
    long Revision,
    Guid? LastAppliedEventId);
