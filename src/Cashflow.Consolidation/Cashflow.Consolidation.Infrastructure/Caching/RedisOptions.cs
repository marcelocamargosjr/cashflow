namespace Cashflow.Consolidation.Infrastructure.Caching;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = "localhost:6379";

    public int DailyTtlSeconds { get; set; } = 60;

    public int StampedeLockTtlSeconds { get; set; } = 5;
}
