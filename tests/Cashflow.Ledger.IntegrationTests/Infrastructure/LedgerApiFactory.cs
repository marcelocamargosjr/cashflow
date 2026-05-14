using Cashflow.Ledger.Infrastructure.Persistence;
using Cashflow.SharedKernel.Time;
using Cashflow.TestSupport;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cashflow.Ledger.IntegrationTests.Infrastructure;

/// <summary>
/// WebApplicationFactory for the Ledger API in integration tests.
///
/// Beyond the standard <see cref="WebApplicationFactory{TEntryPoint}"/>, this:
/// 1. Wires Postgres / RabbitMQ to the ephemeral Testcontainers endpoints from
///    <see cref="CashflowFixture"/>.
/// 2. Replaces JwtBearer config with the local symmetric key so tests can mint
///    their own tokens (<see cref="TestTokens"/>).
/// 3. Exposes <see cref="EnsureSchemaAsync"/> which applies EF migrations against
///    Postgres directly — independent of the host lifecycle. The factory must
///    have its DB ready BEFORE the first request, because production migrations
///    only auto-run under the <c>Development</c> environment.
/// </summary>
public sealed class LedgerApiFactory : WebApplicationFactory<Cashflow.Ledger.Api.Program>
{
    private readonly CashflowFixture _fixture;

    public LedgerApiFactory(CashflowFixture fixture)
    {
        _fixture = fixture;
    }

    public string PostgresConnectionString => _fixture.Postgres.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.Sources.Clear();
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = _fixture.Postgres.GetConnectionString(),
                ["RabbitMq:Host"] = _fixture.Rabbit.Hostname,
                ["RabbitMq:Port"] = _fixture.Rabbit.GetMappedPublicPort(5672).ToString(),
                ["RabbitMq:VirtualHost"] = "/",
                ["RabbitMq:Username"] = "guest",
                ["RabbitMq:Password"] = "guest",
                // Keycloak block is read at boot — point it at the test issuer so the
                // section is not null. The actual handler is rebound by
                // JwtTestAuthentication.ReplaceJwtForTests below.
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

    /// <summary>
    /// Applies EF migrations directly against the test Postgres container, without
    /// going through the host's service provider. This avoids any timing issue with
    /// MassTransit's BusOutbox initialization and lets us guarantee the schema is
    /// ready before tests fire their first request.
    /// </summary>
    public async Task EnsureSchemaAsync()
    {
        var opts = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseNpgsql(PostgresConnectionString, npg => npg.MigrationsHistoryTable("__EFMigrationsHistory", "ledger"))
            .Options;
#pragma warning disable ASP0000 // intentional: standalone DbContext to migrate before host start.
        await using var db = new LedgerDbContext(opts, new ServiceCollection().BuildServiceProvider(), new SystemClock());
#pragma warning restore ASP0000
        await db.Database.MigrateAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Stops the MassTransit IBusControl. Used by tests that want to prove the API
    /// stays up even when downstream messaging is unavailable — OutboxMessage rows
    /// must keep being written transactionally regardless of broker health.
    /// </summary>
    public async Task StopBusAsync()
    {
        var bus = Services.GetRequiredService<IBusControl>();
        await bus.StopAsync().ConfigureAwait(false);
    }
}
