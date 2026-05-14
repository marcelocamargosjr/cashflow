using System.Net;
using System.Net.Http.Json;
using Cashflow.Ledger.IntegrationTests.Infrastructure;
using Cashflow.TestSupport;

namespace Cashflow.Ledger.IntegrationTests.Tests;

public sealed class IsolationNfrTests : LedgerIntegrationTestBase
{
    public IsolationNfrTests(CashflowFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Post_WhenConsolidationDown_StillReturns201_For50Entries()
    {
        var client = CreateAuthenticatedClient(out _);
        var statuses = new List<HttpStatusCode>(capacity: 50);

        for (var i = 0; i < 50; i++)
        {
            // Each POST needs a fresh Idempotency-Key — same key would short-circuit
            // into a replay and not exercise the persistence path.
            client.DefaultRequestHeaders.Remove("Idempotency-Key");
            client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());

            var response = await client.PostAsJsonAsync("/api/v1/entries", new
            {
                type = "Credit",
                amount = new { value = 10m, currency = "BRL" },
                description = $"chaos {i}",
                category = "Sales",
                entryDate = "2026-05-13"
            });

            statuses.Add(response.StatusCode);
        }

        statuses.Should()
            .HaveCount(50)
            .And.AllSatisfy(s => s.Should().Be(HttpStatusCode.Created,
                "NFR demands 100% success on Ledger writes even when Consolidation is offline"));
    }
}
