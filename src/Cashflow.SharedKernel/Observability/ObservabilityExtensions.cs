using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Logs;
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
    /// Matches `07-INFRA-E-DEVOPS.md §2.1` and `§2.2`.
    /// </summary>
    public static WebApplicationBuilder AddCashflowObservability(
        this WebApplicationBuilder builder,
        string serviceName,
        string serviceVersion)
    {
        var otelEndpoint = builder.Configuration[OtelEndpointConfigKey] ?? DefaultOtelEndpoint;
        var environment = builder.Environment.EnvironmentName;

        ConfigureSerilog(builder, serviceName, serviceVersion, otelEndpoint, environment);
        ConfigureOpenTelemetry(builder.Services, serviceName, serviceVersion, otelEndpoint, environment);

        return builder;
    }

    private static void ConfigureSerilog(
        WebApplicationBuilder builder,
        string serviceName,
        string serviceVersion,
        string otelEndpoint,
        string environment)
    {
        builder.Host.UseSerilog((ctx, lc) => lc
            .ReadFrom.Configuration(ctx.Configuration)
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
                o.ResourceAttributes = new Dictionary<string, object>
                {
                    ["service.name"] = serviceName,
                    ["service.version"] = serviceVersion,
                    ["deployment.environment"] = environment
                };
            }));
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
