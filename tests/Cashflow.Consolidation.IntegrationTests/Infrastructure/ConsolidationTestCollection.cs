using Cashflow.TestSupport;

namespace Cashflow.Consolidation.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class ConsolidationTestCollection : ICollectionFixture<CashflowFixture>
{
    public const string Name = "consolidation-integration";
}
