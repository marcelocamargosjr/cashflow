using Cashflow.Ledger.Application.Entries.Queries.GetEntry;
using Cashflow.Ledger.UnitTests.Builders;
using Cashflow.Ledger.UnitTests.TestDoubles;
using Cashflow.SharedKernel.Results;

namespace Cashflow.Ledger.UnitTests.Application;

public class GetEntryQueryHandlerTests
{
    [Fact]
    public async Task Handle_ExistingEntryForCallerMerchant_ShouldReturnDto()
    {
        var repo = new InMemoryEntryRepository();
        var entry = new EntryBuilder().Build();
        await repo.AddAsync(entry, CancellationToken.None);

        var handler = new GetEntryQueryHandler(repo);
        var result = await handler.Handle(
            new GetEntryQuery(entry.Id, entry.MerchantId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(entry.Id);
    }

    [Fact]
    public async Task Handle_NonExistentEntry_ShouldReturnNotFound()
    {
        var repo = new InMemoryEntryRepository();

        var handler = new GetEntryQueryHandler(repo);
        var result = await handler.Handle(
            new GetEntryQuery(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_EntryOfDifferentMerchant_ShouldReturnNotFound()
    {
        var repo = new InMemoryEntryRepository();
        var entry = new EntryBuilder().Build();
        await repo.AddAsync(entry, CancellationToken.None);

        var handler = new GetEntryQueryHandler(repo);
        var result = await handler.Handle(
            new GetEntryQuery(entry.Id, Guid.NewGuid()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public void Validator_RejectsEmptyIds()
    {
        var validator = new GetEntryQueryValidator();

        var validation = validator.Validate(new GetEntryQuery(Guid.Empty, Guid.Empty));

        validation.IsValid.Should().BeFalse();
    }
}
