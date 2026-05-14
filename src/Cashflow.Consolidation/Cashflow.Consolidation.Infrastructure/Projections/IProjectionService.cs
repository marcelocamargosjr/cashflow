using Cashflow.Contracts.V1;

namespace Cashflow.Consolidation.Infrastructure.Projections;

/// <summary>
/// Applies integration events to the daily balance projection atomically.
/// Implementations MUST be idempotent via the <c>lastAppliedEventId</c> guard
/// (see <c>05-DADOS.md §2.5</c> and patch C1 in <c>14-PATCHES-CIRURGICOS.md</c>).
/// </summary>
public interface IProjectionService
{
    /// <summary>
    /// Apply an <see cref="EntryRegisteredV1"/> to the projection. Returns
    /// <c>true</c> when the projection was actually mutated; <c>false</c> when
    /// the guard blocked (event already applied).
    /// </summary>
    Task<bool> ApplyRegistrationAsync(EntryRegisteredV1 evt, CancellationToken cancellationToken);

    /// <summary>
    /// Apply an <see cref="EntryReversedV1"/> to the projection. Returns
    /// <c>true</c> when the projection was actually mutated; <c>false</c> when
    /// the guard blocked or the target document does not yet exist.
    /// </summary>
    Task<bool> ApplyReversalAsync(EntryReversedV1 evt, CancellationToken cancellationToken);
}
