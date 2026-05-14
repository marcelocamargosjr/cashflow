using Cashflow.TestSupport;

namespace Cashflow.Ledger.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class LedgerTestCollection : ICollectionFixture<CashflowFixture>
{
    public const string Name = "ledger-integration";
}
