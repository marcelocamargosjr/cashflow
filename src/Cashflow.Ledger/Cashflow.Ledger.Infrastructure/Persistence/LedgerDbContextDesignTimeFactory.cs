using Cashflow.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace Cashflow.Ledger.Infrastructure.Persistence;

// Used only by `dotnet ef` to materialize the DbContext at design time.
// Em design-time não executamos comandos, então passamos um IServiceProvider
// vazio — qualquer GetService<IPublishEndpoint>() retornaria null, mas
// SaveChangesAsync nunca é chamado pelas ferramentas do EF.
internal sealed class LedgerDbContextDesignTimeFactory : IDesignTimeDbContextFactory<LedgerDbContext>
{
    public LedgerDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5432;Database=cashflow_ledger;Username=cashflow;Password=cashflow";

        var options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseNpgsql(connectionString, npg => npg.MigrationsHistoryTable("__EFMigrationsHistory", "ledger"))
            .Options;

        return new LedgerDbContext(options, new ServiceCollection().BuildServiceProvider(), new SystemClock());
    }
}
