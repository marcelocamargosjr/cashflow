using Cashflow.Consolidation.Application.Balances.Dtos;
using Cashflow.Consolidation.Application.Balances.Queries.GetDailyBalance;
using Cashflow.SharedKernel.Results;
using Cashflow.SharedKernel.Time;
using MediatR;

namespace Cashflow.Consolidation.Application.Balances.Queries.GetCurrentBalance;

/// <summary>
/// Delegates to <see cref="GetDailyBalanceQuery"/> using <c>TodayInBusinessZone</c>.
/// Caching/staleness behavior is inherited from the daily handler.
/// </summary>
public sealed class GetCurrentBalanceQueryHandler
    : IRequestHandler<GetCurrentBalanceQuery, Result<DailyBalanceDto>>
{
    private readonly ISender _sender;
    private readonly IClock _clock;

    public GetCurrentBalanceQueryHandler(ISender sender, IClock clock)
    {
        _sender = sender;
        _clock = clock;
    }

    public Task<Result<DailyBalanceDto>> Handle(
        GetCurrentBalanceQuery request,
        CancellationToken cancellationToken)
    {
        var today = _clock.TodayInBusinessZone;
        return _sender.Send(new GetDailyBalanceQuery(request.MerchantId, today), cancellationToken);
    }
}
