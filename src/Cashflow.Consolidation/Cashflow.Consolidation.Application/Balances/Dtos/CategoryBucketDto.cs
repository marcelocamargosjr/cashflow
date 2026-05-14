namespace Cashflow.Consolidation.Application.Balances.Dtos;

public sealed record CategoryBucketDto(
    string Category,
    decimal Credit,
    decimal Debit,
    int Count);
