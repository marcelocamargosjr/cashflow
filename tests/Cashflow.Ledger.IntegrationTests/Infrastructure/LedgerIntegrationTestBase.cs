using System.Net.Http.Headers;
using Cashflow.TestSupport;
using Respawn;

namespace Cashflow.Ledger.IntegrationTests.Infrastructure;

[Collection(LedgerTestCollection.Name)]
public abstract class LedgerIntegrationTestBase : IAsyncLifetime
{
    protected CashflowFixture Fixture { get; }
    protected LedgerApiFactory Factory { get; }
    private Respawner? _respawner;

    protected LedgerIntegrationTestBase(CashflowFixture fixture)
    {
        Fixture = fixture;
        Factory = new LedgerApiFactory(fixture);
    }

    public async Task InitializeAsync()
    {
        await Factory.EnsureSchemaAsync().ConfigureAwait(false);
        _respawner = await DatabaseReset
            .CreatePostgresRespawnerAsync(Factory.PostgresConnectionString)
.ConfigureAwait(false);
        await DatabaseReset
            .ResetPostgresAsync(_respawner, Factory.PostgresConnectionString)
.ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync().ConfigureAwait(false);
    }

    protected HttpClient CreateAuthenticatedClient(out Guid merchantId, Guid? pinnedMerchant = null)
    {
        merchantId = pinnedMerchant ?? Guid.NewGuid();
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokens.MerchantToken(merchantId));
        return client;
    }
}
