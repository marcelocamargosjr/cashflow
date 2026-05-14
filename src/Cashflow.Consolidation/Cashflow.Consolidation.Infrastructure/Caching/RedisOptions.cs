namespace Cashflow.Consolidation.Infrastructure.Caching;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    /// <summary>StackExchange.Redis connection string.</summary>
    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>Daily balance cache TTL in seconds. Default 60s per <c>05-DADOS.md §3.2</c>.</summary>
    public int DailyTtlSeconds { get; set; } = 60;

    /// <summary>Stampede lock TTL in seconds. Default 5s per <c>05-DADOS.md §3.2</c>.</summary>
    public int StampedeLockTtlSeconds { get; set; } = 5;
}
