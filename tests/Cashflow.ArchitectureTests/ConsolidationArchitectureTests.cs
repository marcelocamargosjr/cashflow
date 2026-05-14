using Cashflow.Consolidation.Application.Balances.Queries.GetDailyBalance;
using Cashflow.Consolidation.Domain.Projections;
using MediatR;
using NetArchTest.Rules;

namespace Cashflow.ArchitectureTests;

/// <summary>
/// Structural invariants for the Consolidation bounded context.
/// </summary>
public sealed class ConsolidationArchitectureTests
{
    private const string Infrastructure = "Cashflow.Consolidation.Infrastructure";
    private const string Api = "Cashflow.Consolidation.Api";

    [Fact]
    public void Domain_DoesNotDependOnInfrastructureOrApi()
    {
        var result = Types.InAssembly(typeof(DailyBalanceReadModel).Assembly)
            .Should()
            .NotHaveDependencyOnAny(Infrastructure, Api)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Consolidation.Domain must stay free of Infrastructure/Api. "
                   + "Offending types: " + string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Application_DoesNotDependOnInfrastructureOrApi()
    {
        var result = Types.InAssembly(typeof(GetDailyBalanceQuery).Assembly)
            .Should()
            .NotHaveDependencyOnAny(Infrastructure, Api)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Consolidation.Application must not pull Infrastructure/Api. "
                   + "Offending types: " + string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Handlers_AreInternalSealed_AndEndWithHandler()
    {
        var handlerInterface = typeof(IRequestHandler<,>);

        var handlerTypes = Types.InAssembly(typeof(GetDailyBalanceQuery).Assembly)
            .That()
            .ImplementInterface(handlerInterface)
            .GetTypes()
            .ToList();

        handlerTypes.Should().NotBeEmpty("Consolidation.Application must expose at least one query handler");

        foreach (var handler in handlerTypes)
        {
            handler.Name.Should().EndWith("Handler");
            handler.IsSealed.Should().BeTrue($"{handler.Name} must be sealed");
            handler.IsPublic.Should().BeFalse($"{handler.Name} must NOT be public");
        }
    }

    [Fact]
    public void Api_DoesNotUseMongoDriverDirectly()
    {
        // Endpoints must go through Application/Infrastructure abstractions, never touch
        // the Mongo driver directly — keeps the persistence boundary intact.
        var result = Types.InAssembly(typeof(Cashflow.Consolidation.Api.Program).Assembly)
            .That()
            .HaveNameEndingWith("Endpoints", StringComparison.Ordinal).Or().HaveNameEndingWith("Controller", StringComparison.Ordinal)
            .Should()
            .NotHaveDependencyOn("MongoDB.Driver")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "API endpoints must not depend on MongoDB.Driver directly. "
                   + "Offending types: " + string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }
}
