using Cashflow.SharedKernel.Results;

namespace Cashflow.Ledger.Application.Entries;

public static class LedgerErrors
{
    public static Error EntryNotFound { get; } =
        Error.NotFound("entry.not_found", "Entry was not found");

    public static Error IdempotencyConflict { get; } =
        Error.Conflict("entry.idempotency_conflict", "Idempotency-Key reused with a different body");

    public static Error AlreadyReversed { get; } =
        Error.Conflict("entry.already_reversed", "Entry is already reversed");

    public static Error InvalidRange { get; } =
        Error.Validation("entries.invalid_range", "from must be less than or equal to to");

    public static Error RangeTooWide { get; } =
        Error.Validation("entries.range_too_wide", "from..to range cannot exceed 90 days");
}
