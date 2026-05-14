using Cashflow.SharedKernel.Domain;
using Cashflow.SharedKernel.Domain.ValueObjects;

namespace Cashflow.Ledger.Domain.Entries;

public sealed class Entry : AggregateRoot
{
    public Guid MerchantId { get; private set; }
    public EntryType Type { get; private set; }
    public Money Amount { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string? Category { get; private set; }
    public DateOnly EntryDate { get; private set; }
    public EntryStatus Status { get; private set; }
    public Guid IdempotencyKey { get; private set; }
    public string IdempotencyBodyHash { get; private set; } = string.Empty;
    public string? ReversalReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Entry() { }

    public static Entry Register(
        Guid merchantId,
        EntryType type,
        Money amount,
        string description,
        string? category,
        DateOnly entryDate,
        Guid idempotencyKey,
        string idempotencyBodyHash,
        DateOnly today,
        DateTimeOffset now)
    {
        if (merchantId == Guid.Empty)
            throw new DomainException("entry.merchant_required", "MerchantId is required");
        if (idempotencyKey == Guid.Empty)
            throw new DomainException("entry.idempotency_key_required", "IdempotencyKey is required");
        if (string.IsNullOrWhiteSpace(idempotencyBodyHash))
            throw new DomainException("entry.idempotency_hash_required", "IdempotencyBodyHash is required");
        if (amount.Value <= 0)
            throw new DomainException("entry.amount_not_positive", "Amount must be greater than zero");
        if (entryDate > today.AddDays(1))
            throw new DomainException("entry.entry_date_future", "EntryDate cannot be in the future beyond today+1");
        if (string.IsNullOrWhiteSpace(description) || description.Length > 200)
            throw new DomainException("entry.description_invalid", "Description must be between 1 and 200 characters");
        if (category is { Length: > 50 })
            throw new DomainException("entry.category_too_long", "Category must be at most 50 characters");

        var entry = new Entry
        {
            Id = Guid.CreateVersion7(),
            MerchantId = merchantId,
            Type = type,
            Amount = amount,
            Description = description,
            Category = category,
            EntryDate = entryDate,
            Status = EntryStatus.Confirmed,
            IdempotencyKey = idempotencyKey,
            IdempotencyBodyHash = idempotencyBodyHash,
            CreatedAt = now,
            UpdatedAt = now,
        };

        entry.Raise(new EntryRegisteredDomainEvent(entry));
        return entry;
    }

    public void Reverse(string reason, DateTimeOffset now)
    {
        if (Status == EntryStatus.Reversed)
            throw new DomainException("entry.already_reversed", "Entry is already reversed");
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
            throw new DomainException("entry.reason_invalid", "Reason must be between 1 and 500 characters");

        Status = EntryStatus.Reversed;
        ReversalReason = reason;
        UpdatedAt = now;

        Raise(new EntryReversedDomainEvent(this));
    }
}
