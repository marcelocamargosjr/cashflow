using Cashflow.Ledger.Application.Entries.Queries.ListEntries;
using Cashflow.Ledger.Domain.Entries;
using Cashflow.Ledger.UnitTests.Builders;
using Cashflow.Ledger.UnitTests.TestDoubles;

namespace Cashflow.Ledger.UnitTests.Application;

public class ListEntriesQueryHandlerTests
{
    private static readonly DateOnly From = new(2026, 5, 1);
    private static readonly DateOnly To = new(2026, 5, 31);

    [Fact]
    public async Task Handle_FiltersByMerchantTypeAndCategory()
    {
        var repo = new InMemoryEntryRepository();
        var merchantId = Guid.NewGuid();
        var other = Guid.NewGuid();

        await repo.AddAsync(new EntryBuilder()
            .WithMerchantId(merchantId).WithType(EntryType.Credit).WithCategory("Sales")
            .WithEntryDate(new(2026, 5, 10)).WithToday(To).Build(), CancellationToken.None);
        await repo.AddAsync(new EntryBuilder()
            .WithMerchantId(merchantId).WithType(EntryType.Debit).WithCategory("Suppliers")
            .WithEntryDate(new(2026, 5, 11)).WithToday(To).Build(), CancellationToken.None);
        await repo.AddAsync(new EntryBuilder()
            .WithMerchantId(other).WithType(EntryType.Credit).WithCategory("Sales")
            .WithEntryDate(new(2026, 5, 10)).WithToday(To).Build(), CancellationToken.None);

        var handler = new ListEntriesQueryHandler(repo);
        var result = await handler.Handle(
            new ListEntriesQuery(merchantId, From, To, EntryType.Credit, "Sales", 1, 50),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Total.Should().Be(1);
        result.Value.HasNext.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_PaginationShouldRespectPageAndSize()
    {
        var repo = new InMemoryEntryRepository();
        var merchantId = Guid.NewGuid();
        for (var i = 0; i < 5; i++)
        {
            await repo.AddAsync(new EntryBuilder()
                .WithMerchantId(merchantId)
                .WithEntryDate(new(2026, 5, 1 + i))
                .WithToday(To)
                .Build(), CancellationToken.None);
        }

        var handler = new ListEntriesQueryHandler(repo);
        var result = await handler.Handle(
            new ListEntriesQuery(merchantId, From, To, null, null, 1, 2),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Total.Should().Be(5);
        result.Value.HasNext.Should().BeTrue();
    }

    [Fact]
    public void Validator_RejectsRangeWiderThan90Days()
    {
        var validator = new ListEntriesQueryValidator();

        var query = new ListEntriesQuery(
            Guid.NewGuid(),
            new(2026, 1, 1),
            new(2026, 6, 1),
            null, null, 1, 50);

        var validation = validator.Validate(query);
        validation.IsValid.Should().BeFalse();
    }
}
