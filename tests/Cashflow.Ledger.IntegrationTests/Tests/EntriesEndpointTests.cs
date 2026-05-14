using System.Net;
using System.Net.Http.Json;
using Cashflow.Ledger.Infrastructure.Persistence;
using Cashflow.Ledger.IntegrationTests.Infrastructure;
using Cashflow.TestSupport;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cashflow.Ledger.IntegrationTests.Tests;

public sealed class EntriesEndpointTests : LedgerIntegrationTestBase
{
    public EntriesEndpointTests(CashflowFixture fixture) : base(fixture) { }

    private static object NewValidEntry(string description = "Counter sale") => new
    {
        type = "Credit",
        amount = new { value = 100m, currency = "BRL" },
        description,
        category = "Sales",
        entryDate = "2026-05-13"
    };

    // IT-01: POST /entries persists in Postgres and writes OutboxMessage in the
    // same transaction. We assert both rows exist after a single successful POST.
    [Fact]
    public async Task Post_ValidEntry_Returns201_AndWritesOutbox()
    {
        var client = CreateAuthenticatedClient(out _);
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await client.PostAsJsonAsync("/api/v1/entries", NewValidEntry());

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();
        (await db.Entries.CountAsync()).Should().Be(1);
        (await db.Set<OutboxMessage>().CountAsync())
            .Should().BeGreaterThan(0, "the EF Outbox must capture EntryRegisteredV1 in the same transaction");
    }

    // IT-02: A replay with the same Idempotency-Key AND the same body returns 201
    // with the `Idempotent-Replayed: true` header. Only one row exists in DB and
    // its `IdempotencyBodyHash` is set (same hash on both attempts).
    [Fact]
    public async Task Post_SameKeyAndBody_Replays201_WithReplayHeader()
    {
        var client = CreateAuthenticatedClient(out _);
        var key = Guid.NewGuid().ToString();
        var payload = NewValidEntry();

        client.DefaultRequestHeaders.Add("Idempotency-Key", key);
        var first = await client.PostAsJsonAsync("/api/v1/entries", payload);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync("/api/v1/entries", payload);

        second.StatusCode.Should().Be(HttpStatusCode.Created);
        second.Headers.TryGetValues("Idempotent-Replayed", out var values).Should().BeTrue();
        values!.Should().ContainSingle().And.Contain("true");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();
        var rows = await db.Entries.ToListAsync();
        rows.Should().ContainSingle("idempotent replay must not create a second row");
        rows[0].IdempotencyBodyHash.Should().NotBeNullOrWhiteSpace();
    }

    // IT-03: Same Idempotency-Key but a divergent body returns 409 Conflict with
    // the canonical `/errors/conflict` ProblemDetails type — comparison is by
    // SHA-256 of the canonical body per `05 §1.3.1`.
    [Fact]
    public async Task Post_SameKeyDifferentBody_Returns409Conflict()
    {
        var client = CreateAuthenticatedClient(out _);
        var key = Guid.NewGuid().ToString();

        client.DefaultRequestHeaders.Add("Idempotency-Key", key);
        var first = await client.PostAsJsonAsync("/api/v1/entries", NewValidEntry("original"));
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync("/api/v1/entries", NewValidEntry("mutated"));

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await second.Content.ReadAsStringAsync();
        problem.Should().Contain("/errors/conflict");
    }
}
