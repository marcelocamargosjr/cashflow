using Cashflow.Consolidation.Application.Balances.Dtos;
using Cashflow.SharedKernel.Results;
using MediatR;

namespace Cashflow.Consolidation.Application.Balances.Queries.GetCurrentBalance;

/// <summary>
/// Returns the accumulated balance for the current business day.
/// Resolved via <see cref="Cashflow.SharedKernel.Time.IClock.TodayInBusinessZone"/>.
/// </summary>
public sealed record GetCurrentBalanceQuery(Guid MerchantId) : IRequest<Result<DailyBalanceDto>>;
