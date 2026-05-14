using Cashflow.Ledger.Domain.Abstractions;
using Cashflow.Ledger.Domain.Entries;
using Cashflow.Ledger.Infrastructure.Persistence;
using Cashflow.Ledger.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cashflow.Ledger.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddLedgerInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres missing");

        // O overload (sp, opt) é essencial: o BusOutbox do MassTransit registra um
        // ISaveChangesInterceptor que precisa ser conectado às DbContextOptions —
        // sem o AddInterceptors, o Publish dispara direto ao broker sem passar
        // pelo OutboxMessage (perde a garantia transacional).
        services.AddDbContext<LedgerDbContext>((sp, opt) =>
        {
            opt.UseNpgsql(connectionString, npg =>
            {
                npg.MigrationsHistoryTable("__EFMigrationsHistory", "ledger");
                npg.EnableRetryOnFailure(maxRetryCount: 3);
            });
            opt.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
        });

        services.AddScoped<IEntryRepository, EntryRepository>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<LedgerDbContext>());

        return services;
    }
}
