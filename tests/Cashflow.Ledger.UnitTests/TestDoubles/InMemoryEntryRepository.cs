using Cashflow.Ledger.Domain.Entries;

namespace Cashflow.Ledger.UnitTests.TestDoubles;

public sealed class InMemoryEntryRepository : IEntryRepository
{
    private readonly List<Entry> _entries = new();

    public IReadOnlyList<Entry> All => _entries;

    public Task AddAsync(Entry entry, CancellationToken cancellationToken = default)
    {
        _entries.Add(entry);
        return Task.CompletedTask;
    }

    public Task<Entry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_entries.FirstOrDefault(e => e.Id == id));

    public Task<Entry?> GetByIdempotencyKeyAsync(
        Guid merchantId,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_entries.FirstOrDefault(e =>
            e.MerchantId == merchantId && e.IdempotencyKey == idempotencyKey));

    public IQueryable<Entry> Query() => _entries.AsQueryable();
}
