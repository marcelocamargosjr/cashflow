using Cashflow.Ledger.Api.Authorization;
using Cashflow.Ledger.Api.Contracts;
using Cashflow.Ledger.Application.Entries.Commands.RegisterEntry;
using Cashflow.Ledger.Application.Entries.Commands.ReverseEntry;
using Cashflow.Ledger.Application.Entries.Dtos;
using Cashflow.Ledger.Application.Entries.Queries.GetEntry;
using Cashflow.Ledger.Application.Entries.Queries.ListEntries;
using Cashflow.Ledger.Domain.Entries;
using Cashflow.SharedKernel.Http;
using Cashflow.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Cashflow.Ledger.Api.Endpoints;

internal static class EntriesEndpoints
{
    public static IEndpointRouteBuilder MapEntriesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/entries")
            .WithTags("entries")
            .RequireAuthorization(AuthorizationPolicies.RequireMerchant);

        group.MapPost("/", RegisterEntryAsync)
            .WithName("RegisterEntry")
            .AddEndpointFilter<IdempotencyKeyEndpointFilter>();

        group.MapGet("/", ListEntriesAsync)
            .WithName("ListEntries");

        group.MapGet("/{id:guid}", GetEntryAsync)
            .WithName("GetEntry");

        group.MapPost("/{id:guid}/reverse", ReverseEntryAsync)
            .WithName("ReverseEntry")
            .AddEndpointFilter<IdempotencyKeyEndpointFilter>();

        return app;
    }

    private static async Task<IResult> RegisterEntryAsync(
        [FromBody] RegisterEntryRequest body,
        [FromHeader(Name = "Idempotency-Key")] Guid idempotencyKey,
        HttpContext httpContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var merchantId = httpContext.GetMerchantId();
        if (merchantId is null)
            return Results.Problem(ProblemDetailsExtensions.Unauthorized("merchantId claim missing", httpContext));

        if (!Enum.TryParse<EntryType>(body.Type, ignoreCase: true, out var type))
            return Results.Problem(ProblemDetailsExtensions.ValidationProblem(
                $"Unsupported entry type: '{body.Type}'", httpContext: httpContext));

        var command = new RegisterEntryCommand(
            MerchantId: merchantId.Value,
            IdempotencyKey: idempotencyKey,
            Type: type,
            Amount: body.Amount.Value,
            Currency: body.Amount.Currency,
            Description: body.Description,
            Category: body.Category,
            EntryDate: body.EntryDate);

        var result = await sender.Send(command, cancellationToken).ConfigureAwait(false);

        return result.Match<IResult>(
            onSuccess: success =>
            {
                if (success.Replayed)
                    httpContext.Response.Headers["Idempotent-Replayed"] = "true";

                return Results.Created($"/api/v1/entries/{success.Entry.Id}", success.Entry);
            },
            onFailure: error => Results.Problem(ProblemDetailsExtensions.FromError(error, httpContext)));
    }

    private static async Task<IResult> ListEntriesAsync(
        HttpContext httpContext,
        ISender sender,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] string? type,
        [FromQuery] string? category,
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int size = 50)
    {
        var merchantId = httpContext.GetMerchantId();
        if (merchantId is null)
            return Results.Problem(ProblemDetailsExtensions.Unauthorized("merchantId claim missing", httpContext));

        EntryType? parsedType = null;
        if (!string.IsNullOrWhiteSpace(type))
        {
            if (!Enum.TryParse<EntryType>(type, ignoreCase: true, out var t))
                return Results.Problem(ProblemDetailsExtensions.ValidationProblem(
                    $"Unsupported entry type: '{type}'", httpContext: httpContext));
            parsedType = t;
        }

        var query = new ListEntriesQuery(merchantId.Value, from, to, parsedType, category, page, size);
        var result = await sender.Send(query, cancellationToken).ConfigureAwait(false);

        return result.Match<IResult>(
            onSuccess: Results.Ok,
            onFailure: error => Results.Problem(ProblemDetailsExtensions.FromError(error, httpContext)));
    }

    private static async Task<IResult> GetEntryAsync(
        Guid id,
        HttpContext httpContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var merchantId = httpContext.GetMerchantId();
        if (merchantId is null)
            return Results.Problem(ProblemDetailsExtensions.Unauthorized("merchantId claim missing", httpContext));

        var result = await sender.Send(new GetEntryQuery(id, merchantId.Value), cancellationToken)
            .ConfigureAwait(false);

        return result.Match<IResult>(
            onSuccess: Results.Ok,
            onFailure: error => Results.Problem(ProblemDetailsExtensions.FromError(error, httpContext)));
    }

    private static async Task<IResult> ReverseEntryAsync(
        Guid id,
        [FromBody] ReverseEntryRequest body,
        [FromHeader(Name = "Idempotency-Key")] Guid idempotencyKey,
        HttpContext httpContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        _ = idempotencyKey; // header presença já validada pelo filter

        var merchantId = httpContext.GetMerchantId();
        if (merchantId is null)
            return Results.Problem(ProblemDetailsExtensions.Unauthorized("merchantId claim missing", httpContext));

        var result = await sender
            .Send(new ReverseEntryCommand(id, merchantId.Value, body.Reason), cancellationToken)
            .ConfigureAwait(false);

        return result.Match<IResult>(
            onSuccess: Results.Ok,
            onFailure: error => Results.Problem(ProblemDetailsExtensions.FromError(error, httpContext)));
    }
}
