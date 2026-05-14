using Cashflow.SharedKernel.Domain;
using Cashflow.SharedKernel.Http;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Cashflow.Ledger.Api.Infrastructure;

internal sealed class ExceptionToProblemDetailsMiddleware : IMiddleware
{
    private readonly ILogger<ExceptionToProblemDetailsMiddleware> _logger;

    public ExceptionToProblemDetailsMiddleware(ILogger<ExceptionToProblemDetailsMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context).ConfigureAwait(false);
        }
        catch (ValidationException ex)
        {
            _logger.LogInformation(ex, "Validation failure");
            var errors = ex.Errors
                .GroupBy(e => string.IsNullOrEmpty(e.PropertyName) ? "_" : e.PropertyName, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray(), StringComparer.Ordinal);

            await WriteProblemAsync(
                context,
                ProblemDetailsExtensions.ValidationProblem("Validation failed.", errors, context)).ConfigureAwait(false);
        }
        catch (DomainException ex)
        {
            _logger.LogInformation(ex, "Domain rule violation");
            await WriteProblemAsync(
                context,
                ProblemDetailsExtensions.ValidationProblem(ex.Message, httpContext: context)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteProblemAsync(
                context,
                ProblemDetailsExtensions.Internal("An unexpected error occurred.", context)).ConfigureAwait(false);
        }
    }

    private static Task WriteProblemAsync(HttpContext context, ProblemDetails problem)
    {
        context.Response.Clear();
        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";
        return context.Response.WriteAsJsonAsync(problem);
    }
}
