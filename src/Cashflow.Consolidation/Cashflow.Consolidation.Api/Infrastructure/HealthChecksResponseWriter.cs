using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Cashflow.Consolidation.Api.Infrastructure;

internal static class HealthChecksResponseWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static Task WriteAsync(HttpContext httpContext, HealthReport report)
    {
        httpContext.Response.ContentType = "application/json";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration,
            results = report.Entries.ToDictionary(
                kv => kv.Key,
                kv => new
                {
                    status = kv.Value.Status.ToString(),
                    description = kv.Value.Description,
                    duration = kv.Value.Duration,
                    tags = kv.Value.Tags,
                    error = kv.Value.Exception?.Message
                }, StringComparer.Ordinal)
        };

        return httpContext.Response.WriteAsJsonAsync(payload, Options);
    }
}
