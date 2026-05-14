using Cashflow.Ledger.Domain.Abstractions;

namespace Cashflow.Ledger.UnitTests.TestDoubles;

public sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveChangesCalls { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCalls++;
        return Task.FromResult(1);
    }
}
