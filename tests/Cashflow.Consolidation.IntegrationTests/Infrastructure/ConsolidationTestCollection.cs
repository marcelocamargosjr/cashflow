using Cashflow.TestSupport;

namespace Cashflow.Consolidation.IntegrationTests.Infrastructure;

// CA1711 silenciado: sufixo "Collection" é exigido pela convenção xUnit
//   ([CollectionDefinition] + [Collection(...)] pareiam por typename).
#pragma warning disable CA1711
[CollectionDefinition(Name)]
public sealed class ConsolidationTestCollection : ICollectionFixture<CashflowFixture>
{
    public const string Name = "consolidation-integration";
}
#pragma warning restore CA1711
