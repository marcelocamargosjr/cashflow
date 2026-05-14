using Cashflow.TestSupport;

namespace Cashflow.Ledger.IntegrationTests.Infrastructure;

/// <summary>
/// xUnit collection definition for Ledger integration tests. Tests share the
/// <see cref="CashflowFixture"/> (Postgres + RabbitMQ + Mongo + Redis containers)
/// and run serially to keep ephemeral resources stable.
///
/// The CollectionDefinition must live in the test assembly (xUnit doesn't discover
/// it across assemblies — see xUnit1041); the support project only carries the
/// shared <see cref="CashflowFixture"/> implementation.
/// </summary>
[CollectionDefinition(Name)]
public sealed class LedgerTestCollection : ICollectionFixture<CashflowFixture>
{
    public const string Name = "ledger-integration";
}
