using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Formatting.Compact;
using Serilog.Sinks.OpenTelemetry;

namespace Cashflow.SharedKernel.Observability;

public static class ObservabilityExtensions
{
    private const string DefaultOtelEndpoint = "http://otel-collector:4317";
    private const string OtelEndpointConfigKey = "OTel:Endpoint";

    /// <summary>
    /// Configures OpenTelemetry traces + metrics + logs and Serilog with OTLP sink.
    /// Works on both <see cref="Microsoft.AspNetCore.Builder.WebApplicationBuilder"/> and
    /// <see cref="HostApplicationBuilder"/> (the worker host) — they share
    /// <see cref="IHostApplicationBuilder"/>. Matches `07-INFRA-E-DEVOPS.md §2.1` and `§2.2`.
    /// </summary>
    public static IHostApplicationBuilder AddCashflowObservability(
        this IHostApplicationBuilder builder,
        string serviceName,
        string serviceVersion)
    {
        var raw = builder.Configuration[OtelEndpointConfigKey];
        // appsettings.json declares values like `${OTEL_EXPORTER_OTLP_ENDPOINT}`. In
        // production those are resolved by docker-compose's env_file substitution; in
        // tests / dev they may stay as the literal placeholder. Treat any unresolved
        // placeholder OR empty value as "fall back to default" — passing the literal
        // to `new Uri(...)` would throw UriFormatException at startup.
        var otelEndpoint = !string.IsNullOrWhiteSpace(raw) && !raw.StartsWith("${", StringComparison.Ordinal)
            ? raw
            : DefaultOtelEndpoint;
        var environment = builder.Environment.EnvironmentName;

        ConfigureSerilog(builder, serviceName, serviceVersion, otelEndpoint, environment);
        ConfigureOpenTelemetry(builder.Services, serviceName, serviceVersion, otelEndpoint, environment);

        return builder;
    }

    private static void ConfigureSerilog(
        IHostApplicationBuilder builder,
        string serviceName,
        string serviceVersion,
        string otelEndpoint,
        string environment)
    {
        var logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithSpan()
            .Enrich.WithProperty("service.name", serviceName)
            .Enrich.WithProperty("service.version", serviceVersion)
            .Enrich.WithProperty("deployment.environment", environment)
            .WriteTo.Console(new CompactJsonFormatter())
            .WriteTo.OpenTelemetry(o =>
            {
                o.Endpoint = otelEndpoint;
                o.Protocol = OtlpProtocol.Grpc;
                o.ResourceAttributes = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["service.name"] = serviceName,
                    ["service.version"] = serviceVersion,
                    ["deployment.environment"] = environment
                };
            })
            .CreateLogger();

        // Static Log.Logger is the fallback used by Serilog.Context (LogContext)
        // and by code paths that bypass the DI logger factory.
        Log.Logger = logger;

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(logger, dispose: true);
    }

    private static void ConfigureOpenTelemetry(
        IServiceCollection services,
        string serviceName,
        string serviceVersion,
        string otelEndpoint,
        string environment)
    {
        services
            .AddOpenTelemetry()
            .ConfigureResource(r => r
                .AddService(serviceName, serviceVersion: serviceVersion)
                .AddAttributes(new[]
                {
                    new KeyValuePair<string, object>("deployment.environment", environment)
                }))
            .WithTracing(t => t
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSource("MassTransit")
                .AddSource("Cashflow.*")
                .AddOtlpExporter(o => o.Endpoint = new Uri(otelEndpoint)))
            .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter("Cashflow.*")
                .AddOtlpExporter(o => o.Endpoint = new Uri(otelEndpoint)));
    }
}
