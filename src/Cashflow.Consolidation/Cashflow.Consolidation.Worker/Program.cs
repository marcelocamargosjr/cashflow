using Cashflow.Consolidation.Infrastructure;
using Cashflow.Consolidation.Worker.Consumers;
using Cashflow.SharedKernel.Observability;
using Cashflow.SharedKernel.Time;
using MassTransit;

var builder = Host.CreateApplicationBuilder(args);

builder.AddCashflowObservability("cashflow.consolidation.worker", "1.0.0");

builder.Services.AddSingleton<IClock, SystemClock>();

builder.Services.AddConsolidationInfrastructure(builder.Configuration);

// ===== MassTransit consumers + Rabbit (06 §3.3, §3.4) =====
// We use the manual ReceiveEndpoint route (Opção A): full control over prefetch,
// retry policy, and DLQ binding per consumer. ConfigureEndpoints is INTENTIONALLY
// not called — mixing the two would create duplicate endpoints (`§3.4`).
builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();

    x.AddConsumer<EntryRegisteredConsumer>();
    x.AddConsumer<EntryReversedConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        var host = builder.Configuration["RabbitMq:Host"] ?? "localhost";
        var vhost = builder.Configuration["RabbitMq:VirtualHost"] ?? "/";
        var user = builder.Configuration["RabbitMq:Username"] ?? "cashflow";
        var pwd = builder.Configuration["RabbitMq:Password"] ?? "cashflow";

        cfg.Host(host, vhost, h =>
        {
            h.Username(user);
            h.Password(pwd);
        });

        cfg.ReceiveEndpoint("consolidation-entry-registered", e =>
        {
            e.PrefetchCount = 32;
            e.ConcurrentMessageLimit = 16;
            e.UseMessageRetry(r =>
            {
                r.Immediate(2);
                r.Interval(3, TimeSpan.FromSeconds(5));
                r.Exponential(
                    3,
                    TimeSpan.FromSeconds(10),
                    TimeSpan.FromMinutes(2),
                    TimeSpan.FromSeconds(15));
            });
            e.ConfigureConsumer<EntryRegisteredConsumer>(ctx);
            e.BindDeadLetterQueue("cashflow.dlx", "cashflow.dlq");
        });

        cfg.ReceiveEndpoint("consolidation-entry-reversed", e =>
        {
            e.PrefetchCount = 32;
            e.ConcurrentMessageLimit = 16;
            e.UseMessageRetry(r => r.Exponential(
                5,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromMinutes(2),
                TimeSpan.FromSeconds(10)));
            e.ConfigureConsumer<EntryReversedConsumer>(ctx);
            e.BindDeadLetterQueue("cashflow.dlx", "cashflow.dlq");
        });

        // Intentionally NO cfg.ConfigureEndpoints(ctx) — duplicates the endpoints above.
    });
});

var host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
