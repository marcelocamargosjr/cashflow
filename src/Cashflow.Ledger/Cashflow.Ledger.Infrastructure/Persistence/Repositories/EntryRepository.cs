using Cashflow.Ledger.Domain.Entries;
using Microsoft.EntityFrameworkCore;

namespace Cashflow.Ledger.Infrastructure.Persistence.Repositories;

internal sealed class EntryRepository : IEntryRepository
{
    private readonly LedgerDbContext _dbContext;

    public EntryRepository(LedgerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Entry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await _dbContext.Entries.AddAsync(entry, cancellationToken).ConfigureAwait(false);
    }

    public Task<Entry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.Entries.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public Task<Entry?> GetByIdempotencyKeyAsync(
        Guid merchantId,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default)
        => _dbContext.Entries
            .FirstOrDefaultAsync(
                e => e.MerchantId == merchantId && e.IdempotencyKey == idempotencyKey,
                cancellationToken);

    public IQueryable<Entry> Query() => _dbContext.Entries.AsNoTracking();
}
