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
(StringComparer.Ordinal)
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

    public async Task EnsureSchemaAsync()
    {
        var opts = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseNpgsql(PostgresConnectionString, npg => npg.MigrationsHistoryTable("__EFMigrationsHistory", "ledger"))
            .Options;
#pragma warning disable ASP0000 // intentional: standalone DbContext to migrate before host start.
        var db = new LedgerDbContext(opts, new ServiceCollection().BuildServiceProvider(), new SystemClock());
#pragma warning restore ASP0000
        await using (db.ConfigureAwait(false))
        {
            await db.Database.MigrateAsync().ConfigureAwait(false);
        }
    }

    public async Task StopBusAsync()
    {
        var bus = Services.GetRequiredService<IBusControl>();
        await bus.StopAsync().ConfigureAwait(false);
    }
}
