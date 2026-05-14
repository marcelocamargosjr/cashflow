using Cashflow.Ledger.Domain.Entries;

namespace Cashflow.Ledger.Application.Entries.Dtos;

public sealed record EntryDto(
    Guid Id,
    Guid MerchantId,
    string Type,
    MoneyDto Amount,
    string Description,
    string? Category,
    DateOnly EntryDate,
    string Status,
    string? ReversalReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static EntryDto FromEntity(Entry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new EntryDto(
            entry.Id,
            entry.MerchantId,
            entry.Type.ToString(),
            new MoneyDto(entry.Amount.Value, entry.Amount.Currency.ToString()),
            entry.Description,
            entry.Category,
            entry.EntryDate,
            entry.Status.ToString(),
            entry.ReversalReason,
            entry.CreatedAt,
            entry.UpdatedAt);
    }
}
