using Cashflow.Consolidation.Infrastructure.Persistence.Documents;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Cashflow.Consolidation.Infrastructure.Persistence;

public sealed class MongoContext
{
    private static int _conventionsRegistered;

    public MongoContext(IMongoClient client, IOptions<MongoOptions> options)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);

        RegisterGlobalConventionsOnce();

        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.Database))
            throw new InvalidOperationException("Mongo:Database is required");

        var database = client.GetDatabase(opts.Database);
        DailyBalances = database.GetCollection<DailyBalanceDoc>(opts.DailyBalancesCollection);
        ProcessedEvents = database.GetCollection<ProcessedEventDoc>(opts.ProcessedEventsCollection);
    }

    public IMongoCollection<DailyBalanceDoc> DailyBalances { get; }

    public IMongoCollection<ProcessedEventDoc> ProcessedEvents { get; }

    private static void RegisterGlobalConventionsOnce()
    {
        if (Interlocked.Exchange(ref _conventionsRegistered, 1) == 1)
            return;

        BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

        var pack = new ConventionPack
        {
            new CamelCaseElementNameConvention(),
            new IgnoreExtraElementsConvention(true)
        };
        ConventionRegistry.Register("cashflow-conventions", pack, _ => true);
    }
}
