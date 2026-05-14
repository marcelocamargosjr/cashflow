using Cashflow.Ledger.Application.Entries.Commands.RegisterEntry;
using Cashflow.Ledger.Domain.Entries;
using Cashflow.Ledger.Infrastructure.Persistence;
using Cashflow.SharedKernel.Domain;
using MediatR;
using NetArchTest.Rules;

namespace Cashflow.ArchitectureTests;

/// <summary>
/// Structural invariants for the Ledger bounded context (`08-TESTES.md §5`).
/// These are the contract the rest of the suite relies on — anything that breaks
/// here means the layering or domain encapsulation slipped.
/// </summary>
public sealed class LedgerArchitectureTests
{
    private const string Infrastructure = "Cashflow.Ledger.Infrastructure";
    private const string Api = "Cashflow.Ledger.Api";

    [Fact]
    public void Domain_DoesNotDependOnInfrastructureOrApi()
    {
        var result = Types.InAssembly(typeof(Entry).Assembly)
            .Should()
            .NotHaveDependencyOnAny(Infrastructure, Api)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Domain must stay pure — no dependency on Infrastructure or Api. "
                   + "Offending types: " + string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Application_DoesNotDependOnInfrastructureOrApi()
    {
        var result = Types.InAssembly(typeof(RegisterEntryCommand).Assembly)
            .Should()
            .NotHaveDependencyOnAny(Infrastructure, Api)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Application must not reference Infrastructure or Api. "
                   + "Offending types: " + string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Handlers_AreInternalSealed_AndEndWithHandler()
    {
        var handlerInterface = typeof(IRequestHandler<,>);

        // ImplementInterface uses the OPEN generic form because that's how NetArchTest
        // matches handlers; the actual closed instantiation is what each handler declares.
        var handlerTypes = Types.InAssembly(typeof(RegisterEntryCommand).Assembly)
            .That()
            .ImplementInterface(handlerInterface)
            .GetTypes()
            .ToList();

        handlerTypes.Should().NotBeEmpty("the application assembly must expose at least one MediatR handler");

        foreach (var handler in handlerTypes)
        {
            handler.Name.Should().EndWith("Handler", because: $"{handler.Name} is a MediatR handler");
            handler.IsSealed.Should().BeTrue($"{handler.Name} must be sealed");
            handler.IsPublic.Should().BeFalse($"{handler.Name} must NOT be public — only the command/query are part of the public Application surface");
        }
    }

    [Fact]
    public void Entities_HaveNoPublicSetters()
    {
        var entityTypes = Types.InAssembly(typeof(Entry).Assembly)
            .That()
            .Inherit(typeof(Entity))
            .GetTypes()
            .ToList();

        entityTypes.Should().NotBeEmpty("the Domain assembly must declare at least one Entity-derived type");

        foreach (var type in entityTypes)
        {
            foreach (var prop in type.GetProperties())
            {
                var setter = prop.GetSetMethod(nonPublic: false);
                setter.Should().BeNull(
                    $"{type.Name}.{prop.Name} must not have a public setter — Entities mutate only via aggregate behaviors");
            }
        }
    }

    [Fact]
    public void Api_DoesNotUseEntityFrameworkCoreDirectly()
    {
        // Endpoints and Controllers in the Api assembly must not reference EF Core —
        // persistence is the Infrastructure layer's job, exposed via repositories or
        // application queries. The DbContext is wired up in Program for DI only.
        var endpointTypes = Types.InAssembly(typeof(Cashflow.Ledger.Api.Program).Assembly)
            .That()
            .HaveNameEndingWith("Endpoints", StringComparison.Ordinal).Or().HaveNameEndingWith("Controller", StringComparison.Ordinal)
            .GetTypes()
            .ToList();

        if (endpointTypes.Count == 0)
            return; // nothing to check yet — the harness for minimal APIs may live in static classes

        var result = Types.InAssembly(typeof(Cashflow.Ledger.Api.Program).Assembly)
            .That()
            .HaveNameEndingWith("Endpoints", StringComparison.Ordinal).Or().HaveNameEndingWith("Controller", StringComparison.Ordinal)
            .Should()
            .NotHaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "API endpoints/controllers must not touch DbContext directly. "
                   + "Offending types: " + string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Domain_DoesNotDependOnMediatR()
    {
        // Defense-in-depth: even though it depends on Application not Mediator directly,
        // a wandering using of MediatR in Domain would be a structural smell. Domain
        // is technology-free.
        var result = Types.InAssembly(typeof(Entry).Assembly)
            .Should()
            .NotHaveDependencyOn("MediatR")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Domain must remain technology-free and cannot reference MediatR. "
                   + "Offending types: " + string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Infrastructure_DoesNotDependOnApi()
    {
        var result = Types.InAssembly(typeof(LedgerDbContext).Assembly)
            .Should()
            .NotHaveDependencyOn(Api)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Infrastructure cannot depend on the Api layer. "
                   + "Offending types: " + string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }
}
