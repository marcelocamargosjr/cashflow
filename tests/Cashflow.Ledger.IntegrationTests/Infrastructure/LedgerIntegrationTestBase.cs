using System.Net.Http.Headers;
using Cashflow.TestSupport;
using Respawn;

namespace Cashflow.Ledger.IntegrationTests.Infrastructure;

/// <summary>
/// Per-test base class that owns the <see cref="LedgerApiFactory"/> for that test
/// and resets Postgres tables (via Respawn) between tests.
///
/// Each integration test class gets its own factory so we can drive isolated
/// behaviour (e.g. <c>StopBusAsync</c> on the IT-08 case) without poisoning the
/// other tests in the suite. The shared <see cref="CashflowFixture"/> remains
/// reused across tests — containers are heavy and reset is cheap.
/// </summary>
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
        await Factory.EnsureSchemaAsync();
        _respawner = await DatabaseReset
            .CreatePostgresRespawnerAsync(Factory.PostgresConnectionString)
            ;
        await DatabaseReset
            .ResetPostgresAsync(_respawner, Factory.PostgresConnectionString)
            ;
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
    }

    /// <summary>
    /// Builds an HttpClient with a fresh merchant token. The token is bound to the
    /// given <paramref name="merchantId"/> (or a new one) so tests can assert
    /// resource-based authorization. The <c>Idempotency-Key</c> header is left
    /// for the caller to set per request.
    /// </summary>
    protected HttpClient CreateAuthenticatedClient(out Guid merchantId, Guid? pinnedMerchant = null)
    {
        merchantId = pinnedMerchant ?? Guid.NewGuid();
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokens.MerchantToken(merchantId));
        return client;
    }
}
