using Bogus;
using Cashflow.Ledger.Domain.Entries;
using Cashflow.SharedKernel.Domain.ValueObjects;

namespace Cashflow.Ledger.UnitTests.Builders;

public sealed class EntryBuilder
{
    private static readonly Faker Faker = new("pt_BR");

    private Guid _merchantId = Guid.NewGuid();
    private EntryType _type = EntryType.Credit;
    private Money _amount = Money.Brl(150.00m);
    private string _description = Faker.Commerce.ProductName();
    private string? _category = "Sales";
    private DateOnly _entryDate = DateOnly.FromDateTime(DateTime.UtcNow.Date);
    private Guid _idempotencyKey = Guid.NewGuid();
    private string _bodyHash = Faker.Random.Hash(64);
    private DateOnly _today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
    private DateTimeOffset _now = DateTimeOffset.UtcNow;

    public EntryBuilder WithMerchantId(Guid merchantId) { _merchantId = merchantId; return this; }
    public EntryBuilder WithType(EntryType type) { _type = type; return this; }
    public EntryBuilder WithAmount(decimal value) { _amount = Money.Brl(value); return this; }
    public EntryBuilder WithAmount(Money amount) { _amount = amount; return this; }
    public EntryBuilder WithDescription(string description) { _description = description; return this; }
    public EntryBuilder WithCategory(string? category) { _category = category; return this; }
    public EntryBuilder WithEntryDate(DateOnly date) { _entryDate = date; return this; }
    public EntryBuilder WithIdempotencyKey(Guid key) { _idempotencyKey = key; return this; }
    public EntryBuilder WithBodyHash(string hash) { _bodyHash = hash; return this; }
    public EntryBuilder WithToday(DateOnly today) { _today = today; return this; }
    public EntryBuilder WithNow(DateTimeOffset now) { _now = now; return this; }

    public Entry Build() =>
        Entry.Register(
            _merchantId,
            _type,
            _amount,
            _description,
            _category,
            _entryDate,
            _idempotencyKey,
            _bodyHash,
            _today,
            _now);
}
