using System.Net;
using System.Net.Http.Headers;
using Cashflow.Consolidation.Infrastructure.Persistence;
using Cashflow.Consolidation.Infrastructure.Persistence.Documents;
using Cashflow.Consolidation.IntegrationTests.Infrastructure;
using Cashflow.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Cashflow.Consolidation.IntegrationTests.Tests;

/// <summary>
/// IT-07 — <c>GET /balances/{merchantId}/daily</c> cache-miss → cache-hit cycle.
///
/// Steps:
///   1. Seed a daily-balance document directly in Mongo.
///   2. First GET: cache empty, Redis key absent before the call; the handler hits
///      Mongo and writes the cache. After the call, the key MUST exist.
///   3. Second GET: handler reads the key from Redis (cache-hit path).
///
/// We don't have a high-fidelity hook to assert "this response came from cache",
/// so we observe the side-effect (the Redis key materialized) plus the response
/// equality between the two calls.
/// </summary>
[Collection(ConsolidationTestCollection.Name)]
public sealed class BalanceCacheTests : IAsyncLifetime
{
    private readonly CashflowFixture _fixture;
    private readonly string _mongoDatabase = $"cashflow_test_{Guid.NewGuid():N}";
    private ConsolidationApiFactory _factory = null!;

    public BalanceCacheTests(CashflowFixture fixture) => _fixture = fixture;

    public Task InitializeAsync()
    {
        _factory = new ConsolidationApiFactory(_fixture, _mongoDatabase);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await DatabaseReset.ResetMongoAsync(_fixture.Mongo.GetConnectionString(), _mongoDatabase);
        await DatabaseReset.ResetRedisAsync(_fixture.Redis.GetConnectionString());
    }

    [Fact]
    public async Task Get_DailyBalance_CacheMissThenCacheHit()
    {
        var merchantId = Guid.NewGuid();
        var date = new DateOnly(2026, 5, 13);

        // Seed Mongo with a daily-balance doc the API can read.
        await SeedBalanceDocAsync(merchantId, date, credits: 500m);

        var cacheKey = $"balance:daily:{merchantId:D}:{date:yyyyMMdd}";
        var redis = await ConnectionMultiplexer.ConnectAsync(_fixture.Redis.GetConnectionString());
        var db = redis.GetDatabase();
        (await db.KeyExistsAsync(cacheKey)).Should().BeFalse("the cache must start empty for this scenario");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokens.MerchantToken(merchantId));

        var first = await client.GetAsync($"/api/v1/balances/{merchantId:D}/daily?date={date:yyyy-MM-dd}");
        var firstBody = await first.Content.ReadAsStringAsync();
        first.StatusCode.Should().Be(HttpStatusCode.OK, "response body was: {0}", firstBody);
        firstBody.Should().Contain("\"hit\":false", "first call must report cache miss");

        (await db.KeyExistsAsync(cacheKey))
            .Should().BeTrue("the handler must populate Redis after a cache-miss read");

        var second = await client.GetAsync($"/api/v1/balances/{merchantId:D}/daily?date={date:yyyy-MM-dd}");
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondBody = await second.Content.ReadAsStringAsync();
        secondBody.Should().Contain("\"hit\":true", "second call must serve from Redis cache");

        // Strip the cache info block from both bodies — that is the only field that
        // legitimately differs between miss and hit; everything else must be byte-equal.
        Normalize(firstBody).Should().Be(Normalize(secondBody),
            "cache-hit must return the same payload as cache-miss (minus cache flags)");

        await redis.DisposeAsync();

        static string Normalize(string body)
        {
            var idx = body.IndexOf(",\"cache\":", StringComparison.Ordinal);
            return idx < 0 ? body : body[..idx];
        }
    }

    private async Task SeedBalanceDocAsync(Guid merchantId, DateOnly date, decimal credits)
    {
        // Resolve MongoContext from the factory before talking to Mongo: MongoContext's
        // constructor registers the cashflow-wide GuidRepresentation.Standard serializer.
        // If we hit a `new MongoClient()` first, the driver would auto-register a default
        // Guid serializer, and the later MongoContext init would throw a duplicate
        // BsonSerializationException.
        using var scope = _factory.Services.CreateScope();
        var mongo = scope.ServiceProvider.GetRequiredService<MongoContext>();

        await mongo.DailyBalances.InsertOneAsync(new DailyBalanceDoc
        {
            Id = DailyBalanceDoc.BuildId(merchantId, date),
            MerchantId = merchantId,
            Date = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            TotalCredits = credits,
            TotalDebits = 0m,
            EntriesCount = 1,
            ByCategory = new List<CategoryBucketDoc>
            {
                new() { Category = "Sales", Credit = credits, Debit = 0m, Count = 1 }
            },
            LastUpdatedAt = DateTime.UtcNow,
            Revision = 1,
            LastAppliedEventId = null
        });
    }
}
