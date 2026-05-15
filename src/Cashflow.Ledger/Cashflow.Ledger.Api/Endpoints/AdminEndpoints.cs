using System.Diagnostics;
using Bogus;
using Cashflow.Ledger.Api.Authorization;
using Cashflow.Ledger.Application.Entries.Commands.RegisterEntry;
using Cashflow.Ledger.Domain.Entries;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Cashflow.Ledger.Api.Endpoints;

internal static class AdminEndpoints
{
    private static readonly string[] Categories =
        ["Sales", "Suppliers", "Taxes", "Payroll", "Marketing", "Utilities", "Refunds"];

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/admin")
            .WithTags("admin")
            .RequireAuthorization(AuthorizationPolicies.RequireAdmin);

        admin.MapPost("/seed", SeedAsync).WithName("AdminSeed");

        return app;
    }

    private static async Task<IResult> SeedAsync(
        [FromBody] SeedRequest body,
        HttpContext httpContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var merchantId = httpContext.GetMerchantId();
        if (merchantId is null)
            return Results.Unauthorized();

        var days = Math.Clamp(body.Days, 1, 365);
        var perDay = Math.Clamp(body.EntriesPerDay, 1, 100);

        // Seed fixo para reprodutibilidade entre runs do seeder no mesmo merchant.
        Bogus.Randomizer.Seed = new Random(0xCA5F10);
        var faker = new Faker();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var sw = Stopwatch.StartNew();
        var created = 0;
        var failed = 0;

        for (var d = 0; d < days; d++)
        {
            var entryDate = today.AddDays(-d);
            for (var i = 0; i < perDay; i++)
            {
                var type = faker.Random.Bool() ? EntryType.Credit : EntryType.Debit;
                var amount = Math.Round((decimal)faker.Random.Double(10, 5000), 2);
                var category = faker.PickRandom(Categories);
                var description = faker.Commerce.ProductName();

                var command = new RegisterEntryCommand(
                    MerchantId: merchantId.Value,
                    IdempotencyKey: Guid.NewGuid(),
                    Type: type,
                    Amount: amount,
                    Currency: "BRL",
                    Description: description,
                    Category: category,
                    EntryDate: entryDate);

                var result = await sender.Send(command, cancellationToken).ConfigureAwait(false);
                if (result.IsSuccess) created++; else failed++;
            }
        }

        sw.Stop();
        return Results.Ok(new SeedResponse(
            Days: days,
            EntriesPerDay: perDay,
            Created: created,
            Failed: failed,
            ElapsedMs: sw.ElapsedMilliseconds));
    }

    private sealed record SeedRequest(int Days = 30, int EntriesPerDay = 20);

    private sealed record SeedResponse(int Days, int EntriesPerDay, int Created, int Failed, long ElapsedMs);
}
