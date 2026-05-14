using Cashflow.TestSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cashflow.Consolidation.IntegrationTests.Infrastructure;

/// <summary>
/// WebApplicationFactory for the Consolidation API. Wires Mongo + Redis to the
/// ephemeral containers and replaces JwtBearer with the local symmetric key so
/// tests can mint <see cref="TestTokens"/> directly without a Keycloak round-trip.
/// </summary>
public sealed class ConsolidationApiFactory : WebApplicationFactory<Cashflow.Consolidation.Api.Program>
{
    private readonly CashflowFixture _fixture;
    private readonly string _mongoDatabase;

    public ConsolidationApiFactory(CashflowFixture fixture, string mongoDatabase)
    {
        _fixture = fixture;
        _mongoDatabase = mongoDatabase;
    }

    public string MongoDatabase => _mongoDatabase;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.Sources.Clear();
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
(StringComparer.Ordinal)
            {
                ["Mongo:ConnectionString"] = _fixture.Mongo.GetConnectionString(),
                ["Mongo:Database"] = _mongoDatabase,
                ["Redis:ConnectionString"] = _fixture.Redis.GetConnectionString(),
                ["RabbitMq:Host"] = _fixture.Rabbit.Hostname,
                ["RabbitMq:Port"] = _fixture.Rabbit.GetMappedPublicPort(5672).ToString(),
                ["Keycloak:Authority"] = TestTokens.Issuer,
                ["Keycloak:Audience"] = TestTokens.Audience,
                ["Keycloak:MetadataAddress"] = TestTokens.Issuer + "/.well-known/openid-configuration",
                ["Keycloak:RequireHttpsMetadata"] = "false",
            });
        });

        builder.ConfigureServices(services =>
        {
            services.ReplaceJwtForTests();
        });
    }
}
