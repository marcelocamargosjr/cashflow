namespace Cashflow.SharedKernel.Domain.ValueObjects;

public readonly record struct Money
{
    public decimal Value { get; }
    public Currency Currency { get; }

    public Money(decimal value, Currency currency = Currency.BRL)
    {
        if (value < 0)
            throw new DomainException("money.negative", "Money value cannot be negative");

        Value = value;
        Currency = currency;
    }

    public static Money Brl(decimal value) => new(value, Currency.BRL);
    public static Money Zero(Currency currency = Currency.BRL) => new(0m, currency);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Value + other.Value, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Value - other.Value, Currency);
    }

    private void EnsureSameCurrency(Money other)
    {
        if (Currency != other.Currency)
            throw new DomainException(
                "money.currency_mismatch",
                $"Cannot operate on Money of different currencies ({Currency} vs {other.Currency})");
    }

    public override string ToString()
        => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{Value:0.00} {Currency}");
}
