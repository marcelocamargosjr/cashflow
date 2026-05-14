using Cashflow.Consolidation.Application.Balances.Dtos;
using Cashflow.SharedKernel.Results;
using MediatR;

namespace Cashflow.Consolidation.Application.Balances.Queries.GetCurrentBalance;

public sealed record GetCurrentBalanceQuery(Guid MerchantId) : IRequest<Result<DailyBalanceDto>>;
