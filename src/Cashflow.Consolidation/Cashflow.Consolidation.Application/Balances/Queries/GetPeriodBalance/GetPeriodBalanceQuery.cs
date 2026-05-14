using Cashflow.Consolidation.Application.Balances.Dtos;
using Cashflow.SharedKernel.Results;
using MediatR;

namespace Cashflow.Consolidation.Application.Balances.Queries.GetPeriodBalance;

public sealed record GetPeriodBalanceQuery(
    Guid MerchantId,
    DateOnly From,
    DateOnly To) : IRequest<Result<PeriodBalanceDto>>;
