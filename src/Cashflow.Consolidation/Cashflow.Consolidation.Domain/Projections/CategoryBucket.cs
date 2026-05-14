namespace Cashflow.Consolidation.Domain.Projections;

public sealed record CategoryBucket(
    string Category,
    decimal Credit,
    decimal Debit,
    int Count);
