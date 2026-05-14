using Cashflow.Contracts.V1;

namespace Cashflow.Consolidation.Infrastructure.Projections;

public interface IProjectionService
{
    Task<bool> ApplyRegistrationAsync(EntryRegisteredV1 evt, CancellationToken cancellationToken);

    Task<bool> ApplyReversalAsync(EntryReversedV1 evt, CancellationToken cancellationToken);
}
