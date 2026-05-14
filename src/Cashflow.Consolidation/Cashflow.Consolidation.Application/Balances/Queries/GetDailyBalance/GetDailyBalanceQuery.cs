using Cashflow.Consolidation.Application.Balances.Dtos;
using Cashflow.SharedKernel.Results;
using MediatR;

namespace Cashflow.Consolidation.Application.Balances.Queries.GetDailyBalance;

public sealed record GetDailyBalanceQuery(
    Guid MerchantId,
    DateOnly Date) : IRequest<Result<DailyBalanceDto>>;
