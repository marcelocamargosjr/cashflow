using Cashflow.Ledger.Application.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cashflow.Ledger.UnitTests.Application;

public class PipelineBehaviorsTests
{
    private sealed record DummyRequest(string Value) : IRequest<string>;

    private sealed class DummyValidator : AbstractValidator<DummyRequest>
    {
        public DummyValidator() => RuleFor(x => x.Value).NotEmpty();
    }

    [Fact]
    public async Task ValidationBehavior_InvalidRequest_ShouldThrowValidationException()
    {
        var behavior = new ValidationBehavior<DummyRequest, string>(new[] { new DummyValidator() });

        var act = () => behavior.Handle(
            new DummyRequest(string.Empty),
            () => Task.FromResult("ok"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task ValidationBehavior_ValidRequest_ShouldCallNext()
    {
        var behavior = new ValidationBehavior<DummyRequest, string>(new[] { new DummyValidator() });

        var response = await behavior.Handle(
            new DummyRequest("value"),
            () => Task.FromResult("ok"),
            CancellationToken.None);

        response.Should().Be("ok");
    }

    [Fact]
    public async Task LoggingBehavior_ShouldNotSwallowExceptionsAndShouldRethrow()
    {
        var behavior = new LoggingBehavior<DummyRequest, string>(NullLogger<LoggingBehavior<DummyRequest, string>>.Instance);

        var act = () => behavior.Handle(
            new DummyRequest("x"),
            () => Task.FromException<string>(new InvalidOperationException("boom")),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task LoggingBehavior_HappyPath_ShouldReturnNextResult()
    {
        var behavior = new LoggingBehavior<DummyRequest, string>(NullLogger<LoggingBehavior<DummyRequest, string>>.Instance);

        var response = await behavior.Handle(
            new DummyRequest("x"),
            () => Task.FromResult("ok"),
            CancellationToken.None);

        response.Should().Be("ok");
    }
}
