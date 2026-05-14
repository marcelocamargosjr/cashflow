using Cashflow.Ledger.Domain.Entries;
using Cashflow.Ledger.UnitTests.Builders;
using Cashflow.SharedKernel.Domain;
using Cashflow.SharedKernel.Domain.ValueObjects;

namespace Cashflow.Ledger.UnitTests.Domain;

public class EntryAggregateTests
{
    private static readonly DateOnly Today = new(2026, 5, 14);
    private static readonly DateTimeOffset Now = new(2026, 5, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Register_HappyPath_ShouldCreateEntryWithConfirmedStatusAndRaiseEvent()
    {
        var merchantId = Guid.NewGuid();
        var idempotencyKey = Guid.NewGuid();

        var entry = new EntryBuilder()
            .WithMerchantId(merchantId)
            .WithType(EntryType.Credit)
            .WithAmount(150m)
            .WithDescription("Counter sale #123")
            .WithCategory("Sales")
            .WithEntryDate(Today)
            .WithIdempotencyKey(idempotencyKey)
            .WithBodyHash(new string('a', 64))
            .WithToday(Today)
            .WithNow(Now)
            .Build();

        entry.Id.Should().NotBe(Guid.Empty);
        entry.MerchantId.Should().Be(merchantId);
        entry.Type.Should().Be(EntryType.Credit);
        entry.Amount.Value.Should().Be(150m);
        entry.Amount.Currency.Should().Be(Currency.BRL);
        entry.Description.Should().Be("Counter sale #123");
        entry.Category.Should().Be("Sales");
        entry.EntryDate.Should().Be(Today);
        entry.Status.Should().Be(EntryStatus.Confirmed);
        entry.IdempotencyKey.Should().Be(idempotencyKey);
        entry.IdempotencyBodyHash.Should().HaveLength(64);
        entry.CreatedAt.Should().Be(Now);
        entry.UpdatedAt.Should().Be(Now);

        entry.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<EntryRegisteredDomainEvent>();
    }

    [Fact]
    public void Register_AmountNegative_BlockedAtMoneyConstruction()
    {
        var act = () => Money.Brl(-0.01m);

        act.Should().Throw<DomainException>()
            .Which.Code.Should().Be("money.negative");
    }

    [Fact]
    public void Register_AmountZero_ShouldThrowDomainException()
    {
        var act = () => new EntryBuilder().WithAmount(Money.Zero()).Build();

        act.Should().Throw<DomainException>()
            .Which.Code.Should().Be("entry.amount_not_positive");
    }

    [Fact]
    public void Register_EntryDateTooFarInFuture_ShouldThrowDomainException()
    {
        var act = () => new EntryBuilder()
            .WithToday(Today)
            .WithEntryDate(Today.AddDays(2))
            .Build();

        act.Should().Throw<DomainException>()
            .Which.Code.Should().Be("entry.entry_date_future");
    }

    [Fact]
    public void Register_EntryDateExactlyTodayPlusOne_ShouldBeAccepted()
    {
        var entry = new EntryBuilder()
            .WithToday(Today)
            .WithEntryDate(Today.AddDays(1))
            .Build();

        entry.EntryDate.Should().Be(Today.AddDays(1));
    }

    [Fact]
    public void Register_DescriptionEmpty_ShouldThrowDomainException()
    {
        var act = () => new EntryBuilder().WithDescription("   ").Build();

        act.Should().Throw<DomainException>()
            .Which.Code.Should().Be("entry.description_invalid");
    }

    [Fact]
    public void Register_DescriptionTooLong_ShouldThrowDomainException()
    {
        var act = () => new EntryBuilder().WithDescription(new string('x', 201)).Build();

        act.Should().Throw<DomainException>()
            .Which.Code.Should().Be("entry.description_invalid");
    }

    [Fact]
    public void Register_DescriptionAt200Chars_ShouldBeAccepted()
    {
        var entry = new EntryBuilder().WithDescription(new string('x', 200)).Build();

        entry.Description.Length.Should().Be(200);
    }

    [Fact]
    public void Register_CategoryTooLong_ShouldThrowDomainException()
    {
        var act = () => new EntryBuilder().WithCategory(new string('c', 51)).Build();

        act.Should().Throw<DomainException>()
            .Which.Code.Should().Be("entry.category_too_long");
    }

    [Fact]
    public void Register_CategoryNull_ShouldBeAccepted()
    {
        var entry = new EntryBuilder().WithCategory(null).Build();

        entry.Category.Should().BeNull();
    }

    [Fact]
    public void Register_EmptyMerchantId_ShouldThrowDomainException()
    {
        var act = () => new EntryBuilder().WithMerchantId(Guid.Empty).Build();

        act.Should().Throw<DomainException>()
            .Which.Code.Should().Be("entry.merchant_required");
    }

    [Fact]
    public void Register_EmptyIdempotencyKey_ShouldThrowDomainException()
    {
        var act = () => new EntryBuilder().WithIdempotencyKey(Guid.Empty).Build();

        act.Should().Throw<DomainException>()
            .Which.Code.Should().Be("entry.idempotency_key_required");
    }

    [Fact]
    public void Register_EmptyIdempotencyBodyHash_ShouldThrowDomainException()
    {
        var act = () => new EntryBuilder().WithBodyHash(string.Empty).Build();

        act.Should().Throw<DomainException>()
            .Which.Code.Should().Be("entry.idempotency_hash_required");
    }

    [Fact]
    public void Reverse_ConfirmedEntry_ShouldTransitionToReversedAndRaiseEvent()
    {
        var entry = new EntryBuilder().Build();
        entry.ClearDomainEvents();

        var later = Now.AddHours(2);
        entry.Reverse("customer dispute", later);

        entry.Status.Should().Be(EntryStatus.Reversed);
        entry.ReversalReason.Should().Be("customer dispute");
        entry.UpdatedAt.Should().Be(later);
        entry.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<EntryReversedDomainEvent>();
    }

    [Fact]
    public void Reverse_AlreadyReversedEntry_ShouldThrowDomainException()
    {
        var entry = new EntryBuilder().Build();
        entry.Reverse("first reason", Now);

        var act = () => entry.Reverse("second reason", Now.AddMinutes(1));

        act.Should().Throw<DomainException>()
            .Which.Code.Should().Be("entry.already_reversed");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Reverse_EmptyReason_ShouldThrowDomainException(string reason)
    {
        var entry = new EntryBuilder().Build();

        var act = () => entry.Reverse(reason, Now);

        act.Should().Throw<DomainException>()
            .Which.Code.Should().Be("entry.reason_invalid");
    }

    [Fact]
    public void Reverse_ReasonTooLong_ShouldThrowDomainException()
    {
        var entry = new EntryBuilder().Build();

        var act = () => entry.Reverse(new string('r', 501), Now);

        act.Should().Throw<DomainException>()
            .Which.Code.Should().Be("entry.reason_invalid");
    }

    [Fact]
    public void EventId_OfRegisteredEvent_ShouldBeNonEmpty()
    {
        var entry = new EntryBuilder().Build();
        var evt = entry.DomainEvents.OfType<EntryRegisteredDomainEvent>().Single();

        evt.EventId.Should().NotBe(Guid.Empty);
        evt.OccurredOn.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        evt.Entry.Should().BeSameAs(entry);
    }

    [Fact]
    public void ClearDomainEvents_ShouldRemoveAllPendingEvents()
    {
        var entry = new EntryBuilder().Build();
        entry.DomainEvents.Should().NotBeEmpty();

        entry.ClearDomainEvents();

        entry.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Id_ShouldBeGuidVersion7()
    {
        var entry = new EntryBuilder().Build();

        // Guid v7 has version nibble = 7 in the version field (byte 7 high nibble in big-endian).
        var bytes = entry.Id.ToByteArray();
        var version = (bytes[7] & 0xF0) >> 4;
        version.Should().Be(7);
    }
}
