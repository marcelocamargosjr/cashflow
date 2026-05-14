using Cashflow.SharedKernel.Http;
using Microsoft.AspNetCore.Http;

namespace Cashflow.Ledger.Api.Endpoints;

// Rejects 400 quando POSTs idempotentes não trazem Idempotency-Key.
// Tipagem do header já é validada via [FromHeader] Guid — aqui só garantimos a presença
// para devolver Problem+JSON em vez do erro genérico de model binding.
internal sealed class IdempotencyKeyEndpointFilter : IEndpointFilter
{
    private const string HeaderName = "Idempotency-Key";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;

        if (!http.Request.Headers.TryGetValue(HeaderName, out var values)
            || string.IsNullOrWhiteSpace(values.ToString()))
        {
            return Results.Problem(ProblemDetailsExtensions.ValidationProblem(
                "Header 'Idempotency-Key' is required for this operation.",
                errors: new Dictionary<string, string[]>
(StringComparer.Ordinal)
                {
                    ["Idempotency-Key"] = new[] { "header is required" }
                },
                httpContext: http));
        }

        if (!Guid.TryParse(values.ToString(), out _))
        {
            return Results.Problem(ProblemDetailsExtensions.ValidationProblem(
                "Header 'Idempotency-Key' must be a valid UUID.",
                errors: new Dictionary<string, string[]>
(StringComparer.Ordinal)
                {
                    ["Idempotency-Key"] = new[] { "must be a UUID" }
                },
                httpContext: http));
        }

        return await next(context).ConfigureAwait(false);
    }
}
