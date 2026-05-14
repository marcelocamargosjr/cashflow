namespace Cashflow.Consolidation.Domain.Projections;

/// <summary>
/// Per-category breakdown inside a <see cref="DailyBalanceReadModel"/>.
/// Counts and totals never go negative in normal use; a fully-reversed bucket is
/// kept (count=0, credit=0, debit=0) for audit trail — never $pull.
/// </summary>
public sealed record CategoryBucket(
    string Category,
    decimal Credit,
    decimal Debit,
    int Count);
