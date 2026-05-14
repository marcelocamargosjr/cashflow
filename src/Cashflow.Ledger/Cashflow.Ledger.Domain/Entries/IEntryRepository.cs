namespace Cashflow.Ledger.Domain.Entries;

public interface IEntryRepository
{
    Task AddAsync(Entry entry, CancellationToken cancellationToken = default);

    Task<Entry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Entry?> GetByIdempotencyKeyAsync(
        Guid merchantId,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default);

    IQueryable<Entry> Query();
}
