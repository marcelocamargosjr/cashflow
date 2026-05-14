using System.Net;
using System.Net.Http.Json;
using Cashflow.Ledger.IntegrationTests.Infrastructure;
using Cashflow.TestSupport;

namespace Cashflow.Ledger.IntegrationTests.Tests;

/// <summary>
/// IT-08 — the NFR-of-isolation regression.
///
/// The literal product requirement: <b>"if Consolidation is down, the Ledger
/// must still return 201 on POST /entries"</b>. In this test setup, "Consolidation
/// is down" is materialized by the fact that no consumer is wired up on the test
/// host — only the Ledger.Api is running, hitting Postgres + RabbitMQ.
///
/// What this proves: events stay queued in the EF Outbox (or the broker) and the
/// API path never blocks waiting for a downstream subscriber. 50 POSTs in tight
/// loop, all 50 must come back 201 Created.
/// </summary>
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
