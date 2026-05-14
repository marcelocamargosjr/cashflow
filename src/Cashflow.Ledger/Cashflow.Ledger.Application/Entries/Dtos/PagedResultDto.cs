namespace Cashflow.Ledger.Application.Entries.Dtos;

public sealed record PagedResultDto<T>(
    IReadOnlyList<T> Items,
    int Page,
    int Size,
    int Total,
    bool HasNext);
