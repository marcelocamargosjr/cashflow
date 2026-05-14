namespace Cashflow.Consolidation.Infrastructure.Persistence;

public sealed class MongoOptions
{
    public const string SectionName = "Mongo";

    public string ConnectionString { get; set; } = string.Empty;

    public string Database { get; set; } = "cashflow_consolidation";

    public string DailyBalancesCollection { get; set; } = "daily_balances";

    public string ProcessedEventsCollection { get; set; } = "processed_events";
}
