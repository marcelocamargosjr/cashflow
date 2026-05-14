using System.Diagnostics;
using Cashflow.SharedKernel.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Cashflow.SharedKernel.Http;

/// <summary>
/// Helpers for RFC 7807 ProblemDetails. The canonical types are listed in
/// <c>04-DOMINIO-E-API.md §5</c>; this class centralizes their construction
/// so every API/Gateway emits the same `type` URIs and statuses.
/// </summary>
public static class ProblemDetailsTypes
{
    public const string BaseUri = "https://cashflow.local/errors/";

    public const string Validation = BaseUri + "validation";
    public const string Unauthorized = BaseUri + "unauthorized";
    public const string Forbidden = BaseUri + "forbidden";
    public const string NotFound = BaseUri + "not-found";
    public const string Conflict = BaseUri + "conflict";
    public const string RateLimit = BaseUri + "rate-limit";
    public const string Internal = BaseUri + "internal";
    public const string DependencyUnavailable = BaseUri + "dependency-unavailable";
}

public static class ProblemDetailsExtensions
{
    public static ProblemDetails ValidationProblem(
        string detail,
        IDictionary<string, string[]>? errors = null,
        HttpContext? httpContext = null)
    {
        var problem = new ValidationProblemDetails(errors ?? new Dictionary<string, string[]>())
        {
            Type = ProblemDetailsTypes.Validation,
            Title = "Validation failed",
            Status = StatusCodes.Status400BadRequest,
            Detail = detail
        };
        Enrich(problem, httpContext);
        return problem;
    }

    public static ProblemDetails Unauthorized(string detail, HttpContext? httpContext = null)
        => Build(ProblemDetailsTypes.Unauthorized, "Unauthorized",
            StatusCodes.Status401Unauthorized, detail, httpContext);

    public static ProblemDetails Forbidden(string detail, HttpContext? httpContext = null)
        => Build(ProblemDetailsTypes.Forbidden, "Forbidden",
            StatusCodes.Status403Forbidden, detail, httpContext);

    public static ProblemDetails NotFound(string detail, HttpContext? httpContext = null)
        => Build(ProblemDetailsTypes.NotFound, "Resource not found",
            StatusCodes.Status404NotFound, detail, httpContext);

    public static ProblemDetails Conflict(string detail, HttpContext? httpContext = null)
        => Build(ProblemDetailsTypes.Conflict, "Conflict",
            StatusCodes.Status409Conflict, detail, httpContext);

    public static ProblemDetails RateLimit(string detail, HttpContext? httpContext = null)
        => Build(ProblemDetailsTypes.RateLimit, "Too many requests",
            StatusCodes.Status429TooManyRequests, detail, httpContext);

    public static ProblemDetails Internal(string detail, HttpContext? httpContext = null)
        => Build(ProblemDetailsTypes.Internal, "Internal server error",
            StatusCodes.Status500InternalServerError, detail, httpContext);

    public static ProblemDetails DependencyUnavailable(string detail, HttpContext? httpContext = null)
        => Build(ProblemDetailsTypes.DependencyUnavailable, "Dependency unavailable",
            StatusCodes.Status503ServiceUnavailable, detail, httpContext);

    public static ProblemDetails FromError(Error error, HttpContext? httpContext = null) => error.Type switch
    {
        ErrorType.Validation => ValidationProblem(error.Message,
            new Dictionary<string, string[]> { [error.Code] = new[] { error.Message } }, httpContext),
        ErrorType.NotFound => NotFound(error.Message, httpContext),
        ErrorType.Conflict => Conflict(error.Message, httpContext),
        ErrorType.Unauthorized => Unauthorized(error.Message, httpContext),
        ErrorType.Forbidden => Forbidden(error.Message, httpContext),
        ErrorType.RateLimit => RateLimit(error.Message, httpContext),
        ErrorType.DependencyUnavailable => DependencyUnavailable(error.Message, httpContext),
        ErrorType.Internal => Internal(error.Message, httpContext),
        _ => Internal(error.Message, httpContext)
    };

    private static ProblemDetails Build(
        string type,
        string title,
        int status,
        string detail,
        HttpContext? httpContext)
    {
        var problem = new ProblemDetails
        {
            Type = type,
            Title = title,
            Status = status,
            Detail = detail
        };
        Enrich(problem, httpContext);
        return problem;
    }

    private static void Enrich(ProblemDetails problem, HttpContext? httpContext)
    {
        if (httpContext is not null)
        {
            problem.Instance = httpContext.Request.Path;
            problem.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;

            if (httpContext.Items.TryGetValue(CorrelationIdMiddleware.HttpContextItemKey, out var corr)
                && corr is string correlation)
            {
                problem.Extensions["correlationId"] = correlation;
            }
        }
        else
        {
            var activityId = Activity.Current?.Id;
            if (!string.IsNullOrEmpty(activityId))
                problem.Extensions["traceId"] = activityId;
        }
    }
}
