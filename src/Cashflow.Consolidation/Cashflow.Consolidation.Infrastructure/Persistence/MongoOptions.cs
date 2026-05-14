namespace Cashflow.Consolidation.Infrastructure.Persistence;

public sealed class MongoOptions
{
    public const string SectionName = "Mongo";

    /// <summary>Mongo connection string (driver URI form, e.g. <c>mongodb://...</c>).</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Database name. Defaults to <c>cashflow_consolidation</c>.</summary>
    public string Database { get; set; } = "cashflow_consolidation";

    /// <summary>Collection name for the daily balances projection.</summary>
    public string DailyBalancesCollection { get; set; } = "daily_balances";

    /// <summary>Collection name for processed events (fast-path idempotency).</summary>
    public string ProcessedEventsCollection { get; set; } = "processed_events";
}
