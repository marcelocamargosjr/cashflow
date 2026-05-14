using Cashflow.SharedKernel.Results;

namespace Cashflow.SharedKernel.UnitTests.Results;

public class ResultTests
{
    [Fact]
    public void Success_ShouldBeSuccessAndExposeValue()
    {
        var result = Result.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be(42);
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void Failure_ShouldBeFailureAndExposeError()
    {
        var error = Error.Validation("entry.amount.invalid", "Amount must be greater than zero");

        var result = Result.Failure<int>(error);

        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void AccessingValueOnFailure_ShouldThrow()
    {
        var result = Result.Failure<int>(Error.Internal("x", "boom"));

        var act = () => _ = result.Value;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ImplicitConversion_FromValue_ShouldYieldSuccess()
    {
        Result<string> result = "hello";

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("hello");
    }

    [Fact]
    public void ImplicitConversion_FromError_ShouldYieldFailure()
    {
        var error = Error.NotFound("entry.not_found", "Entry not found");

        Result<string> result = error;

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Match_ShouldInvokeSuccessBranch_WhenSuccess()
    {
        var result = Result.Success("ok");

        var matched = result.Match(v => $"value:{v}", e => $"error:{e.Code}");

        matched.Should().Be("value:ok");
    }

    [Fact]
    public void Match_ShouldInvokeFailureBranch_WhenFailure()
    {
        var error = Error.Conflict("dup", "duplicated");
        var result = Result.Failure<string>(error);

        var matched = result.Match(v => $"value:{v}", e => $"error:{e.Code}");

        matched.Should().Be("error:dup");
    }

    [Fact]
    public void SuccessConstructor_CannotCarryError()
    {
        var act = () => new ForcedResult(true, Error.Internal("x", "y"));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FailureConstructor_MustCarryError()
    {
        var act = () => new ForcedResult(false, Error.None);

        act.Should().Throw<InvalidOperationException>();
    }

    private sealed class ForcedResult : Result
    {
        public ForcedResult(bool isSuccess, Error error) : base(isSuccess, error) { }
    }
}
