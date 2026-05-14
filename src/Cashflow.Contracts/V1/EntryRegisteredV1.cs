namespace Cashflow.Contracts.V1;

/// <summary>
/// Published by Ledger when an entry is confirmed.
/// Consumed by Consolidation to update the daily projection.
/// </summary>
public sealed record EntryRegisteredV1(
    Guid EventId,
    DateTimeOffset OccurredOn,
    Guid MerchantId,
    Guid EntryId,
    string Type,           // "Credit" | "Debit"
    decimal Amount,        // decimal — NEVER double
    string Currency,       // ISO-4217 alpha-3, e.g. "BRL"
    DateOnly EntryDate,    // business competence date
    string? Category
) : IIntegrationEvent
{
    public int Version => 1;
}
