using Cashflow.Ledger.Application.Entries.Commands.ReverseEntry;
using Cashflow.Ledger.Domain.Entries;
using Cashflow.Ledger.UnitTests.Builders;
using Cashflow.Ledger.UnitTests.TestDoubles;
using Cashflow.SharedKernel.Results;

namespace Cashflow.Ledger.UnitTests.Application;

public class ReverseEntryCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 5, 14);

    [Fact]
    public async Task Handle_ConfirmedEntry_ShouldReverseAndReturnUpdatedDto()
    {
        var repo = new InMemoryEntryRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FakeClock(Now, Today);

        var entry = new EntryBuilder().WithToday(Today).WithEntryDate(Today).Build();
        await repo.AddAsync(entry, CancellationToken.None);

        var handler = new ReverseEntryCommandHandler(repo, uow, clock);
        var result = await handler.Handle(
            new ReverseEntryCommand(entry.Id, entry.MerchantId, "customer dispute"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(nameof(EntryStatus.Reversed));
        result.Value.ReversalReason.Should().Be("customer dispute");
        uow.SaveChangesCalls.Should().Be(1);
    }

    [Fact]
    public async Task Handle_EntryNotFound_ShouldReturnNotFound()
    {
        var repo = new InMemoryEntryRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FakeClock(Now, Today);

        var handler = new ReverseEntryCommandHandler(repo, uow, clock);
        var result = await handler.Handle(
            new ReverseEntryCommand(Guid.NewGuid(), Guid.NewGuid(), "reason"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        uow.SaveChangesCalls.Should().Be(0);
    }

    [Fact]
    public async Task Handle_DifferentMerchant_ShouldReturnNotFound()
    {
        var repo = new InMemoryEntryRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FakeClock(Now, Today);

        var entry = new EntryBuilder().Build();
        await repo.AddAsync(entry, CancellationToken.None);

        var handler = new ReverseEntryCommandHandler(repo, uow, clock);
        var result = await handler.Handle(
            new ReverseEntryCommand(entry.Id, Guid.NewGuid(), "reason"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_AlreadyReversed_ShouldReturnConflict()
    {
        var repo = new InMemoryEntryRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FakeClock(Now, Today);

        var entry = new EntryBuilder().Build();
        entry.Reverse("first", Now);
        await repo.AddAsync(entry, CancellationToken.None);

        var handler = new ReverseEntryCommandHandler(repo, uow, clock);
        var result = await handler.Handle(
            new ReverseEntryCommand(entry.Id, entry.MerchantId, "second"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("entry.already_reversed");
    }

    [Fact]
    public void Validator_RejectsEmptyIdsAndReason()
    {
        var validator = new ReverseEntryCommandValidator();

        var validation = validator.Validate(new ReverseEntryCommand(Guid.Empty, Guid.Empty, string.Empty));

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain(e => e.PropertyName == nameof(ReverseEntryCommand.EntryId));
        validation.Errors.Should().Contain(e => e.PropertyName == nameof(ReverseEntryCommand.MerchantId));
        validation.Errors.Should().Contain(e => e.PropertyName == nameof(ReverseEntryCommand.Reason));
    }
}
