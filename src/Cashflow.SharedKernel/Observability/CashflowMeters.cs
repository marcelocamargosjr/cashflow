using System.Diagnostics.Metrics;

namespace Cashflow.SharedKernel.Observability;

/// <summary>
/// Custom metrics da família <c>Cashflow.*</c> (07 §2.5). Registradas em um
/// único <see cref="Meter"/> exposto via OTel (ver
/// <see cref="ObservabilityExtensions.ConfigureOpenTelemetry"/> — wildcard
/// <c>AddMeter("Cashflow.*")</c>).
///
/// Instrumentos:
/// <list type="bullet">
///   <item><c>cashflow.entries.registered</c> — counter por type/category.</item>
///   <item><c>cashflow.projection.lag.seconds</c> — histogram (now - event.OccurredOn).</item>
///   <item><c>cashflow.cache.hits</c> / <c>cashflow.cache.misses</c> — counters por key_pattern.</item>
///   <item><c>cashflow.idempotency.hits</c> — counter por endpoint.</item>
/// </list>
///
/// Convencionamos nomes em snake/dot — o Prometheus exporter do OTel converte
/// para <c>cashflow_entries_registered_total</c> (sufixo <c>_total</c> automático
/// em counters), <c>cashflow_projection_lag_seconds_bucket</c>, etc. Os dashboards
/// em <c>infra/grafana/dashboards/*</c> consultam esses nomes finais.
/// </summary>
public static class CashflowMeters
{
    public const string MeterName = "Cashflow.Metrics";

    public static readonly Meter Meter = new(MeterName, "1.0.0");

    public static readonly Counter<long> EntriesRegistered = Meter.CreateCounter<long>(
        "cashflow.entries.registered",
        unit: "{entry}",
        description: "Total de lançamentos registrados (Ledger), particionado por type e category.");

    public static readonly Histogram<double> ProjectionLagSeconds = Meter.CreateHistogram<double>(
        "cashflow.projection.lag.seconds",
        unit: "s",
        description: "Lag entre OccurredOn do evento e o momento em que o consumer começa a processar (clock - evt.OccurredOn).");

    public static readonly Counter<long> CacheHits = Meter.CreateCounter<long>(
        "cashflow.cache.hits",
        unit: "{hit}",
        description: "Hits na camada de cache (Redis daily balance, JWKS, etc).");

    public static readonly Counter<long> CacheMisses = Meter.CreateCounter<long>(
        "cashflow.cache.misses",
        unit: "{miss}",
        description: "Misses na camada de cache.");

    public static readonly Counter<long> IdempotencyHits = Meter.CreateCounter<long>(
        "cashflow.idempotency.hits",
        unit: "{hit}",
        description: "Requests com Idempotency-Key que bateram em entrada já persistida (replay).");
}
