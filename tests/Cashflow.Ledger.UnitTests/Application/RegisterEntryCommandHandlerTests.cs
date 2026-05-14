using Cashflow.Ledger.Application.Abstractions.Idempotency;
using Cashflow.Ledger.Application.Entries.Commands.RegisterEntry;
using Cashflow.Ledger.Domain.Entries;
using Cashflow.Ledger.UnitTests.Builders;
using Cashflow.Ledger.UnitTests.TestDoubles;
using Cashflow.SharedKernel.Results;

namespace Cashflow.Ledger.UnitTests.Application;

public class RegisterEntryCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 5, 14);

    private static RegisterEntryCommand DefaultCommand(
        Guid? merchantId = null,
        Guid? idempotencyKey = null,
        decimal amount = 150m) =>
        new(
            merchantId ?? Guid.NewGuid(),
            idempotencyKey ?? Guid.NewGuid(),
            EntryType.Credit,
            amount,
            "BRL",
            "Counter sale #123",
            "Sales",
            Today);

    [Fact]
    public async Task Handle_NewIdempotencyKey_ShouldPersistEntryAndReturnNotReplayed()
    {
        var repo = new InMemoryEntryRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FakeClock(Now, Today);
        var handler = new RegisterEntryCommandHandler(repo, uow, clock);

        var command = DefaultCommand();

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Replayed.Should().BeFalse();
        result.Value.Entry.MerchantId.Should().Be(command.MerchantId);
        result.Value.Entry.Amount.Value.Should().Be(150m);
        repo.All.Should().ContainSingle();
        uow.SaveChangesCalls.Should().Be(1);
    }

    [Fact]
    public async Task Handle_SameKeySameBody_ShouldReturnReplayedWithoutDuplicating()
    {
        var repo = new InMemoryEntryRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FakeClock(Now, Today);
        var handler = new RegisterEntryCommandHandler(repo, uow, clock);

        var merchantId = Guid.NewGuid();
        var key = Guid.NewGuid();
        var first = DefaultCommand(merchantId, key);

        var firstResult = await handler.Handle(first, CancellationToken.None);
        firstResult.IsSuccess.Should().BeTrue();

        var second = DefaultCommand(merchantId, key);
        var replayResult = await handler.Handle(second, CancellationToken.None);

        replayResult.IsSuccess.Should().BeTrue();
        replayResult.Value.Replayed.Should().BeTrue();
        replayResult.Value.Entry.Id.Should().Be(firstResult.Value.Entry.Id);
        repo.All.Should().ContainSingle();
        uow.SaveChangesCalls.Should().Be(1);
    }

    [Fact]
    public async Task Handle_SameKeyDifferentBody_ShouldReturnConflict()
    {
        var repo = new InMemoryEntryRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FakeClock(Now, Today);
        var handler = new RegisterEntryCommandHandler(repo, uow, clock);

        var merchantId = Guid.NewGuid();
        var key = Guid.NewGuid();

        var first = DefaultCommand(merchantId, key, amount: 150m);
        await handler.Handle(first, CancellationToken.None);

        var second = first with { Amount = 999m };
        var result = await handler.Handle(second, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("entry.idempotency_conflict");
        repo.All.Should().ContainSingle();
        uow.SaveChangesCalls.Should().Be(1);
    }

    [Fact]
    public void Validator_RejectsZeroAmountAndEmptyDescription()
    {
        var validator = new RegisterEntryCommandValidator();

        var invalid = DefaultCommand() with { Amount = 0m, Description = string.Empty };
        var validation = validator.Validate(invalid);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterEntryCommand.Amount));
        validation.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterEntryCommand.Description));
    }

    [Fact]
    public void Canonicalizer_ShouldProduceStableHashForSameBody()
    {
        var dto = new IdempotencyCanonicalDto("op", Guid.NewGuid(), Guid.NewGuid(), "body");

        var h1 = RequestCanonicalizer.Hash(dto);
        var h2 = RequestCanonicalizer.Hash(dto);

        h1.Should().Be(h2).And.HaveLength(64);
    }
}
