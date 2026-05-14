using Cashflow.Contracts.V1;
using Cashflow.Ledger.Domain.Entries;
using Cashflow.SharedKernel.Domain;

namespace Cashflow.Ledger.Infrastructure.Messaging;

// Currency goes through CurrencyCode.ToAlpha3((short)numeric) — never .ToString() on the enum.
internal static class IntegrationEventMapper
{
    public static IIntegrationEvent? Map(IDomainEvent domainEvent, DateTimeOffset now) => domainEvent switch
    {
        EntryRegisteredDomainEvent registered => new EntryRegisteredV1(
            EventId: Guid.CreateVersion7(),
            OccurredOn: now,
            MerchantId: registered.Entry.MerchantId,
            EntryId: registered.Entry.Id,
            Type: registered.Entry.Type.ToString(),
            Amount: registered.Entry.Amount.Value,
            Currency: CurrencyCode.ToAlpha3((short)registered.Entry.Amount.Currency),
            EntryDate: registered.Entry.EntryDate,
            Category: registered.Entry.Category),

        EntryReversedDomainEvent reversed => new EntryReversedV1(
            EventId: Guid.CreateVersion7(),
            OccurredOn: now,
            MerchantId: reversed.Entry.MerchantId,
            EntryId: reversed.Entry.Id,
            Type: reversed.Entry.Type.ToString(),
            Amount: reversed.Entry.Amount.Value,
            Currency: CurrencyCode.ToAlpha3((short)reversed.Entry.Amount.Currency),
            EntryDate: reversed.Entry.EntryDate,
            Category: reversed.Entry.Category,
            Reason: reversed.Entry.ReversalReason!),

        _ => null
    };
}
