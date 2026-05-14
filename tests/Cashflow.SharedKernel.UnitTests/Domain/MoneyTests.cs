using Cashflow.SharedKernel.Domain;
using Cashflow.SharedKernel.Domain.ValueObjects;

namespace Cashflow.SharedKernel.UnitTests.Domain;

public class MoneyTests
{
    [Fact]
    public void Brl_ShouldCreateMoneyInBrlCurrency()
    {
        var money = Money.Brl(150.00m);

        money.Value.Should().Be(150.00m);
        money.Currency.Should().Be(Currency.BRL);
    }

    [Fact]
    public void Zero_ShouldBeValid()
    {
        var money = Money.Zero();

        money.Value.Should().Be(0m);
        money.Currency.Should().Be(Currency.BRL);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-100)]
    [InlineData(-9999.99)]
    public void Negative_ShouldThrowDomainException(decimal negative)
    {
        var act = () => new Money(negative);

        act.Should().Throw<DomainException>()
            .Which.Code.Should().Be("money.negative");
    }

    [Fact]
    public void Equality_SameValueAndCurrency_ShouldBeEqual()
    {
        var a = Money.Brl(10);
        var b = Money.Brl(10);

        a.Should().Be(b);
        (a == b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentValue_ShouldNotBeEqual()
    {
        Money.Brl(10).Should().NotBe(Money.Brl(11));
    }

    [Fact]
    public void Add_SameCurrency_ShouldSum()
    {
        var sum = Money.Brl(10).Add(Money.Brl(5));

        sum.Value.Should().Be(15m);
        sum.Currency.Should().Be(Currency.BRL);
    }

    [Fact]
    public void Subtract_LeadingToNegative_ShouldThrow()
    {
        var act = () => Money.Brl(5).Subtract(Money.Brl(10));

        act.Should().Throw<DomainException>()
            .Which.Code.Should().Be("money.negative");
    }

    [Fact]
    public void ToString_ShouldReturnFormattedValueWithCurrency()
    {
        Money.Brl(123.4m).ToString().Should().Be("123.40 BRL");
    }
}
