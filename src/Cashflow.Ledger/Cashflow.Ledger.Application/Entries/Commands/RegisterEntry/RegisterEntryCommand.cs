using Cashflow.Ledger.Application.Abstractions.Idempotency;
using Cashflow.Ledger.Domain.Entries;
using Cashflow.SharedKernel.Results;
using MediatR;

namespace Cashflow.Ledger.Application.Entries.Commands.RegisterEntry;

public sealed record RegisterEntryCommand(
    Guid MerchantId,
    Guid IdempotencyKey,
    EntryType Type,
    decimal Amount,
    string Currency,
    string Description,
    string? Category,
    DateOnly EntryDate) : IRequest<Result<RegisterEntryResult>>
{
    public IdempotencyCanonicalDto ToCanonicalDto() =>
        new(
            "RegisterEntry",
            MerchantId,
            IdempotencyKey,
            $"{(byte)Type}|{Amount.ToString("0.############", System.Globalization.CultureInfo.InvariantCulture)}|{Currency}|{Description}|{Category ?? string.Empty}|{EntryDate:yyyy-MM-dd}");
}
