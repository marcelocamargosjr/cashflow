using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Cashflow.Consolidation.Api.Infrastructure;

internal sealed class MongoHealthCheck : IHealthCheck
{
    private readonly IMongoClient _client;

    public MongoHealthCheck(IMongoClient client)
    {
        _client = client;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var admin = _client.GetDatabase("admin");
            await admin.RunCommandAsync<BsonDocument>(
                new BsonDocument("ping", 1),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return HealthCheckResult.Healthy("ping ok");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Mongo ping failed", ex);
        }
    }
}
