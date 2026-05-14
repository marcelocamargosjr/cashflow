namespace Cashflow.Ledger.Application.Abstractions.Idempotency;

public sealed record IdempotencyCanonicalDto(
    string Operation,
    Guid MerchantId,
    Guid IdempotencyKey,
    string Body);
