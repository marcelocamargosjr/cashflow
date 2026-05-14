using Testcontainers.MongoDb;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;
using Xunit;

namespace Cashflow.TestSupport;

/// <summary>
/// Shared infra fixture for integration tests (`08-TESTES.md §4.2`).
///
/// Spins one ephemeral instance of Postgres / Mongo / RabbitMQ / Redis per xUnit
/// collection. All four start in parallel during <see cref="InitializeAsync"/> and
/// are torn down in parallel during <see cref="DisposeAsync"/>. Tests run inside
/// the collection `cashflow` and share these endpoints — per-test isolation is
/// handled by Respawn (Postgres) and the reset helpers (<see cref="DatabaseReset"/>)
/// for Mongo / Redis.
///
/// Important: we DO NOT bind host ports — Testcontainers always picks free random
/// ports. This lets the suite run on dev boxes where a native Postgres/Mongo is
/// already bound on the default port.
/// </summary>
public sealed class CashflowFixture : IAsyncLifetime
{
    public PostgreSqlContainer Postgres { get; } = new PostgreSqlBuilder()
        .WithImage("postgres:16.3-alpine")
        .WithDatabase("cashflow_ledger")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    // RabbitMq base image generates random credentials by default and the well-known
    // `guest` user is restricted to localhost connections. Pinning the username/password
    // here keeps the connection strings predictable so the fixture can export them as
    // env vars below for the production Program.cs to consume.
    public const string RabbitUser = "cashflow";
    public const string RabbitPassword = "cashflow";

    public RabbitMqContainer Rabbit { get; } = new RabbitMqBuilder()
        .WithImage("rabbitmq:3.13-management")
        .WithUsername(RabbitUser)
        .WithPassword(RabbitPassword)
        .Build();

    public RedisContainer Redis { get; } = new RedisBuilder()
        .WithImage("redis:7.4-alpine")
        .Build();

    public MongoDbContainer Mongo { get; } = new MongoDbBuilder()
        .WithImage("mongo:7.0.12")
        .Build();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(
            Postgres.StartAsync(),
            Rabbit.StartAsync(),
            Redis.StartAsync(),
            Mongo.StartAsync());

        // The production Program.cs reads infra connection strings from
        // configuration *during* its Main body — BEFORE WebApplicationFactory's
        // ConfigureAppConfiguration callbacks ever run. Patching at the
        // builder level after the fact is too late. The robust workaround:
        // export the resolved Testcontainers endpoints as environment
        // variables so the EnvironmentVariablesConfigurationProvider injects
        // them into the configuration at build time (env vars override
        // anything declared in appsettings*.json by default).
        //
        // The `__` separator is the .NET convention for nested keys
        // (e.g. `Keycloak__Authority` maps to `Keycloak:Authority`).
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", Postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("RabbitMq__Host", Rabbit.Hostname);
        Environment.SetEnvironmentVariable("RabbitMq__Port", Rabbit.GetMappedPublicPort(5672).ToString());
        Environment.SetEnvironmentVariable("RabbitMq__VirtualHost", "/");
        Environment.SetEnvironmentVariable("RabbitMq__Username", RabbitUser);
        Environment.SetEnvironmentVariable("RabbitMq__Password", RabbitPassword);
        Environment.SetEnvironmentVariable("Keycloak__Authority", TestTokens.Issuer);
        Environment.SetEnvironmentVariable("Keycloak__Audience", TestTokens.Audience);
        Environment.SetEnvironmentVariable("Keycloak__MetadataAddress", TestTokens.Issuer + "/.well-known/openid-configuration");
        Environment.SetEnvironmentVariable("Keycloak__RequireHttpsMetadata", "false");
        Environment.SetEnvironmentVariable("OTel__Endpoint", "http://localhost:4317");
        Environment.SetEnvironmentVariable("Mongo__ConnectionString", Mongo.GetConnectionString());
        Environment.SetEnvironmentVariable("Mongo__Database", "cashflow_test");
        Environment.SetEnvironmentVariable("Redis__ConnectionString", Redis.GetConnectionString());
    }

    public Task DisposeAsync() => Task.WhenAll(
        Postgres.DisposeAsync().AsTask(),
        Rabbit.DisposeAsync().AsTask(),
        Redis.DisposeAsync().AsTask(),
        Mongo.DisposeAsync().AsTask());
}
