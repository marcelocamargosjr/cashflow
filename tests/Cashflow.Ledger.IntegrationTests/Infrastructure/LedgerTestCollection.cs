using Cashflow.TestSupport;

namespace Cashflow.Ledger.IntegrationTests.Infrastructure;

// CA1711 silenciado: sufixo "Collection" é exigido pela convenção xUnit.
#pragma warning disable CA1711
[CollectionDefinition(Name)]
public sealed class LedgerTestCollection : ICollectionFixture<CashflowFixture>
{
    public const string Name = "ledger-integration";
}
#pragma warning restore CA1711
