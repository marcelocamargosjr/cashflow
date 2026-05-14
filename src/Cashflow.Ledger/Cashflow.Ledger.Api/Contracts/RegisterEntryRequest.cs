namespace Cashflow.Ledger.Api.Contracts;

public sealed record RegisterEntryRequest(
    string Type,
    AmountRequest Amount,
    string Description,
    string? Category,
    DateOnly EntryDate);

public sealed record AmountRequest(decimal Value, string Currency);

public sealed record ReverseEntryRequest(string Reason);
