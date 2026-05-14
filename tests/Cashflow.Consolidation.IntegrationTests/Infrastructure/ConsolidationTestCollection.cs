using Cashflow.TestSupport;

namespace Cashflow.Consolidation.IntegrationTests.Infrastructure;

/// <summary>
/// xUnit collection definition for Consolidation integration tests.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ConsolidationTestCollection : ICollectionFixture<CashflowFixture>
{
    public const string Name = "consolidation-integration";
}
