using Cashflow.Ledger.Infrastructure.Persistence;
using MassTransit;

namespace Cashflow.Ledger.Api.Infrastructure;

internal static class MessagingServiceCollectionExtensions
{
    public static IServiceCollection AddLedgerMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();

            x.AddEntityFrameworkOutbox<LedgerDbContext>(o =>
            {
                o.QueryDelay = TimeSpan.FromSeconds(1);
                o.UsePostgres();
                o.UseBusOutbox();
                o.DuplicateDetectionWindow = TimeSpan.FromHours(24);
            });

            x.UsingRabbitMq((_, cfg) =>
            {
                var host = configuration["RabbitMq:Host"] ?? "localhost";
                var vhost = configuration["RabbitMq:VirtualHost"] ?? "/";
                var user = configuration["RabbitMq:Username"] ?? "cashflow";
                var pwd = configuration["RabbitMq:Password"] ?? "cashflow";

                cfg.Host(host, vhost, h =>
                {
                    h.Username(user);
                    h.Password(pwd);
                });

                cfg.UseMessageRetry(r => r.Exponential(
                    retryLimit: 5,
                    minInterval: TimeSpan.FromSeconds(1),
                    maxInterval: TimeSpan.FromSeconds(30),
                    intervalDelta: TimeSpan.FromSeconds(2)));

                // Producer-only. ConfigureEndpoints intentionally NOT called — Ledger não consome.
            });
        });

        // Ledger é producer-only (sem InboxState — ver ADR-0007). O InboxCleanupService que o
        // AddEntityFrameworkOutbox registra tenta abrir o DbSet<InboxState> a cada ciclo e gera
        // retries barulhentos. Como não temos consumer, desligamos a sweep aqui.
        var inboxCleanupDescriptor = services.FirstOrDefault(d =>
            d.ImplementationType is { Name: "InboxCleanupService`1" } t
            && string.Equals(t.Namespace, "MassTransit.EntityFrameworkCoreIntegration", StringComparison.Ordinal));
        if (inboxCleanupDescriptor is not null)
            services.Remove(inboxCleanupDescriptor);

        return services;
    }
}
