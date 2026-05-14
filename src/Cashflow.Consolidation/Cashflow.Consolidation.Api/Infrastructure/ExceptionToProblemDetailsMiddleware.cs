using Cashflow.SharedKernel.Http;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using StackExchange.Redis;

namespace Cashflow.Consolidation.Api.Infrastructure;

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
                .GroupBy(e => string.IsNullOrEmpty(e.PropertyName) ? "_" : e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            await WriteProblemAsync(
                context,
                ProblemDetailsExtensions.ValidationProblem("Validation failed.", errors, context));
        }
        catch (Exception ex) when (ex is MongoException or RedisException or RedisConnectionException)
        {
            _logger.LogError(ex, "Read-side dependency unavailable");
            var problem = ProblemDetailsExtensions.DependencyUnavailable(
                "A dependency required to read the projection is currently unavailable.", context);
            context.Response.Headers["Retry-After"] = "5";
            await WriteProblemAsync(context, problem);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteProblemAsync(
                context,
                ProblemDetailsExtensions.Internal("An unexpected error occurred.", context));
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
