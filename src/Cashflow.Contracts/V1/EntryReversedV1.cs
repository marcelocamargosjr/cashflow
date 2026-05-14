namespace Cashflow.Contracts.V1;

/// <summary>
/// Published by Ledger when an entry is reversed.
/// Consumed by Consolidation to decrement the corresponding totals.
///
/// Carries a SNAPSHOT of the original entry (Type/Amount/Currency/EntryDate/Category)
/// so the consumer can decrement the projection autonomously, with NO synchronous
/// call back to the Ledger. This preserves NFR-A-01 (Ledger isolation).
/// </summary>
public sealed record EntryReversedV1(
    Guid EventId,
    DateTimeOffset OccurredOn,
    Guid MerchantId,
    Guid EntryId,
    string Type,           // snapshot — "Credit" | "Debit" of the original entry
    decimal Amount,        // snapshot — same value used in EntryRegisteredV1
    string Currency,       // snapshot — ISO 4217 alpha-3, e.g. "BRL"
    DateOnly EntryDate,    // snapshot — locates the daily projection document
    string? Category,      // snapshot — locates the byCategory bucket to decrement
    string Reason
) : IIntegrationEvent
{
    public int Version => 1;
}
