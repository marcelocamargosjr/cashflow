using Cashflow.Consolidation.Application.Abstractions;
using Cashflow.Consolidation.Application.Balances.Dtos;
using Cashflow.SharedKernel.Results;
using MediatR;

namespace Cashflow.Consolidation.Application.Balances.Queries.GetPeriodBalance;

/// <summary>
/// Period balance hits Mongo directly: caching a 31-day window per merchant per request
/// blows up cardinality with little benefit. Daily lookup remains cached individually.
/// </summary>
public sealed class GetPeriodBalanceQueryHandler
    : IRequestHandler<GetPeriodBalanceQuery, Result<PeriodBalanceDto>>
{
    private readonly IDailyBalanceReadRepository _repository;

    public GetPeriodBalanceQueryHandler(IDailyBalanceReadRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<PeriodBalanceDto>> Handle(
        GetPeriodBalanceQuery request,
        CancellationToken cancellationToken)
    {
        if (request.From > request.To)
            return BalanceErrors.InvalidRange;
        if ((request.To.DayNumber - request.From.DayNumber) > 31)
            return BalanceErrors.RangeTooWide;

        var days = await _repository.GetRangeAsync(
            request.MerchantId, request.From, request.To, cancellationToken).ConfigureAwait(false);

        var dailyDtos = days
            .Select(d => DailyBalanceDto.FromReadModel(d, new CacheInfo(false, 0)))
            .ToList();

        var totalCredits = days.Sum(d => d.TotalCredits);
        var totalDebits = days.Sum(d => d.TotalDebits);
        var entriesCount = days.Sum(d => d.EntriesCount);

        return new PeriodBalanceDto(
            request.MerchantId,
            request.From,
            request.To,
            totalCredits,
            totalDebits,
            totalCredits - totalDebits,
            entriesCount,
            dailyDtos);
    }
}
