using Cashflow.Consolidation.Api.Authorization;
using Cashflow.Consolidation.Application.Balances;
using Cashflow.Consolidation.Application.Balances.Queries.GetCurrentBalance;
using Cashflow.Consolidation.Application.Balances.Queries.GetDailyBalance;
using Cashflow.Consolidation.Application.Balances.Queries.GetPeriodBalance;
using Cashflow.SharedKernel.Http;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Cashflow.Consolidation.Api.Endpoints;

internal static class BalancesEndpoints
{
    public static IEndpointRouteBuilder MapBalancesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/balances")
            .WithTags("balances")
            .RequireAuthorization(AuthorizationPolicies.RequireMerchant);

        group.MapGet("/{merchantId:guid}/daily", GetDailyAsync).WithName("GetDailyBalance");
        group.MapGet("/{merchantId:guid}/period", GetPeriodAsync).WithName("GetPeriodBalance");
        group.MapGet("/{merchantId:guid}/current", GetCurrentAsync).WithName("GetCurrentBalance");

        return app;
    }

    private static async Task<IResult> GetDailyAsync(
        Guid merchantId,
        [FromQuery] DateOnly date,
        HttpContext httpContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!httpContext.CanAccessMerchant(merchantId))
            return Results.Problem(ProblemDetailsExtensions.FromError(BalanceErrors.Forbidden, httpContext));

        var result = await sender.Send(new GetDailyBalanceQuery(merchantId, date), cancellationToken)
            .ConfigureAwait(false);

        return result.Match<IResult>(
            onSuccess: Results.Ok,
            onFailure: error => Results.Problem(ProblemDetailsExtensions.FromError(error, httpContext)));
    }

    private static async Task<IResult> GetPeriodAsync(
        Guid merchantId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        HttpContext httpContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!httpContext.CanAccessMerchant(merchantId))
            return Results.Problem(ProblemDetailsExtensions.FromError(BalanceErrors.Forbidden, httpContext));

        var result = await sender.Send(new GetPeriodBalanceQuery(merchantId, from, to), cancellationToken)
            .ConfigureAwait(false);

        return result.Match<IResult>(
            onSuccess: Results.Ok,
            onFailure: error => Results.Problem(ProblemDetailsExtensions.FromError(error, httpContext)));
    }

    private static async Task<IResult> GetCurrentAsync(
        Guid merchantId,
        HttpContext httpContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!httpContext.CanAccessMerchant(merchantId))
            return Results.Problem(ProblemDetailsExtensions.FromError(BalanceErrors.Forbidden, httpContext));

        var result = await sender.Send(new GetCurrentBalanceQuery(merchantId), cancellationToken)
            .ConfigureAwait(false);

        return result.Match<IResult>(
            onSuccess: Results.Ok,
            onFailure: error => Results.Problem(ProblemDetailsExtensions.FromError(error, httpContext)));
    }
}
