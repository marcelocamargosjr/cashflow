namespace Cashflow.Consolidation.Application.Balances.Dtos;

public sealed record PeriodBalanceDto(
    Guid MerchantId,
    DateOnly From,
    DateOnly To,
    decimal TotalCredits,
    decimal TotalDebits,
    decimal Balance,
    int EntriesCount,
    IReadOnlyList<DailyBalanceDto> Days);
