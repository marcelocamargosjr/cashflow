using Cashflow.Consolidation.Infrastructure;
using Cashflow.Consolidation.Worker.Consumers;
using Cashflow.SharedKernel.Time;
using Cashflow.TestSupport;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cashflow.Consolidation.IntegrationTests.Infrastructure;

/// <summary>
/// Lightweight test harness that hosts the Consolidation consumers in-process,
/// pointed at the test RabbitMQ + Mongo containers. Mirrors
/// <c>Cashflow.Consolidation.Worker.Program</c> for messaging plumbing but skips
/// Serilog/OTel bootstrap to keep test logs clean and uses
/// <c>ConfigureEndpoints</c> (vs the prod manual ReceiveEndpoint route) because
/// retry/prefetch tuning is a prod concern not under test in IT-04..IT-06.
/// </summary>
public sealed class ConsolidationWorkerHost : IAsyncDisposable
{
    private readonly IHost _host;

    public string MongoDatabaseName { get; }

    private ConsolidationWorkerHost(IHost host, string database)
    {
        _host = host;
        MongoDatabaseName = database;
    }

    public IServiceProvider Services => _host.Services;

    public static async Task<ConsolidationWorkerHost> StartAsync(
        CashflowFixture fixture,
        string? mongoDatabase = null)
    {
        var database = mongoDatabase ?? $"cashflow_test_{Guid.NewGuid():N}";

        var builder = Host.CreateApplicationBuilder();

        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Mongo:ConnectionString"] = fixture.Mongo.GetConnectionString(),
            ["Mongo:Database"] = database,
        });

        builder.Services.AddSingleton<IClock, SystemClock>();
        builder.Services.AddConsolidationInfrastructure(builder.Configuration);

        builder.Services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();

            x.AddConsumer<EntryRegisteredConsumer>();
            x.AddConsumer<EntryReversedConsumer>();

            x.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(new Uri($"rabbitmq://{fixture.Rabbit.Hostname}:{fixture.Rabbit.GetMappedPublicPort(5672)}"), h =>
                {
                    h.Username(CashflowFixture.RabbitUser);
                    h.Password(CashflowFixture.RabbitPassword);
                });

                cfg.ConfigureEndpoints(ctx);
            });
        });

        var host = builder.Build();
        await host.StartAsync();
        return new ConsolidationWorkerHost(host, database);
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
    }
}
