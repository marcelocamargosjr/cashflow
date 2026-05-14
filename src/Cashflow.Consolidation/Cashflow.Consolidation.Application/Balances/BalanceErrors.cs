using Cashflow.SharedKernel.Results;

namespace Cashflow.Consolidation.Application.Balances;

public static class BalanceErrors
{
    public static Error InvalidRange { get; } =
        Error.Validation("balance.invalid_range", "from must be less than or equal to to");

    public static Error RangeTooWide { get; } =
        Error.Validation("balance.range_too_wide", "from..to range cannot exceed 31 days");

    public static Error Forbidden { get; } =
        Error.Forbidden("balance.forbidden", "Caller is not authorized for the requested merchantId");
}
