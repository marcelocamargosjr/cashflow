using Cashflow.Consolidation.Infrastructure.Persistence;
using Cashflow.TestSupport;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;

namespace Cashflow.Consolidation.IntegrationTests.Infrastructure;

[Collection(ConsolidationTestCollection.Name)]
public abstract class ConsolidationWorkerTestBase : IAsyncLifetime
{
    protected CashflowFixture Fixture { get; }
    protected ConsolidationWorkerHost Host { get; private set; } = null!;
    protected MongoContext Mongo => Host.Services.GetRequiredService<MongoContext>();
    protected IBus Bus => Host.Services.GetRequiredService<IBus>();

    protected ConsolidationWorkerTestBase(CashflowFixture fixture)
    {
        Fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        Host = await ConsolidationWorkerHost.StartAsync(Fixture).ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        await Host.DisposeAsync().ConfigureAwait(false);
        await DatabaseReset.ResetMongoAsync(Fixture.Mongo.GetConnectionString(), Host.MongoDatabaseName).ConfigureAwait(false);
    }
}
