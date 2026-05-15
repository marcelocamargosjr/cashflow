# ADR-0010: OpenTelemetry + Grafana stack (Loki / Tempo / Prometheus) para observabilidade

- **Status:** Accepted
- **Data:** 2026-05-13
- **Decisores:** @marcelo
- **Tags:** `observabilidade`, `otel`, `grafana`, `prometheus`, `loki`, `tempo`

## Contexto e problema

NFR-O-01..05 (MUST/SHOULD) exigem:

- **Logs estruturados** com `correlationId`, `traceId`, `spanId`.
- **Traces distribuídos** end-to-end: Gateway → API → DB → Outbox → Broker → Consumer → Projection.
- **Métricas RED + business + runtime** expostas em `/metrics`.
- **Healthchecks** `/health/live` (self) e `/health/ready` (dependências).
- **Dashboards Grafana** provisionados (mínimo RED + NFR Validation).

A escolha de tecnologia precisa minimizar lock-in, suportar local-dev sem licenças e oferecer correlação de logs ↔ traces ↔ métricas pelo `traceId`.

## Direcionadores da decisão

- **D1.** NFR-O-01..05.
- **D2.** Vendor-neutral — exportar dados em formato aberto (OTLP) e poder trocar o back-end.
- **D3.** Local-dev sem custo de licenças.
- **D4.** Suporte first-class em .NET (`OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`).
- **D5.** Correlação `traceId` ↔ logs ↔ métricas (clicar no log e ver o trace inteiro).

## Alternativas consideradas

### Opção A — Datadog / New Relic / AppDynamics
- SaaS proprietário.
- **Prós:** UX polida.
- **Contras:** licença pesada em prod; conexão de saída exigida em dev; **D2/D3 violados**.

### Opção B — Elastic Stack (Elastic APM + Kibana)
- Bom suporte .NET; APM agent maduro.
- **Contras:** stack mais pesada (JVM + Elasticsearch); licença SSPL não-OSS para módulos avançados; correlação trace ↔ log requer setup específico.

### Opção C — OpenTelemetry + Grafana stack (Loki + Tempo + Prometheus) — **escolhida**
- OTel SDK no app exporta para OTel Collector via OTLP gRPC.
- Collector roteia: logs → Loki, traces → Tempo, métricas → Prometheus.
- Grafana provisiona datasources com link `traceId` → trace, `correlationId` → log search.
- **Prós:** 100% OSS, vendor-neutral, Tempo + Loki + Prometheus muito leves, suporte .NET excelente.
- **Contras:** mais componentes para subir (mas todos com healthchecks e profile `core`).

### Opção D — Jaeger + Prometheus + Loki (sem Tempo)
- **Prós:** Jaeger é maduro.
- **Contras:** Tempo é stateless-ish, mais barato em storage; integração Grafana mais natural; ecosystem da CNCF empurra Tempo.

## Decisão

Escolhemos **OpenTelemetry SDK** nas apps exportando para **OTel Collector** (`otel/opentelemetry-collector-contrib:0.107.0`), que distribui para:

- **Prometheus 2.55** (métricas — scrape pull) — `/metrics` em cada app + receivers do collector.
- **Loki 3.2** (logs) — via `loki` exporter no collector.
- **Tempo 2.6** (traces) — via `otlp` exporter no collector.
- **Grafana 11.2** com datasources/dashboards provisionados em `infra/grafana/provisioning/`.

**SDK no .NET** (em `Cashflow.SharedKernel.Observability.AddCashflowObservability`):

```csharp
services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(serviceName, serviceVersion))
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddNpgsql()
        .AddMassTransitInstrumentation()
        .AddSource("Cashflow.*")
        .AddOtlpExporter(o => o.Endpoint = new Uri(otelEndpoint)))
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddMeter("Cashflow.*")
        .AddRuntimeInstrumentation()
        .AddProcessInstrumentation()
        .AddOtlpExporter(o => o.Endpoint = new Uri(otelEndpoint)));

// Serilog → console (Loki via collector scrape do stdout)
hostBuilder.UseSerilog((ctx, lc) => lc
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithSpan()    // injeta traceId/spanId no log
    .WriteTo.Console(new RenderedCompactJsonFormatter()));
```

**Correlação:**
- `CorrelationIdMiddleware` injeta header `X-Correlation-Id` (gera se ausente) e empurra para `LogContext`.
- `traceId`/`spanId` são injetados via `Serilog.Enrichers.Span`.
- Grafana provisiona link "trace to log" usando `traceId`.

**Dashboards provisionados:**
- `RED` (request rate / error rate / latency) por serviço.
- `NFR Validation` — curva do k6 (50 req/s × 60s), `http_req_duration p95/p99`, `http_req_failed rate`.

## Consequências

### Positivas
- **Vendor-neutral** — trocar Tempo por Jaeger ou Datadog exige só reconfigurar o collector.
- **Stack toda gratuita** — Tempo e Loki são leves (não usam Elasticsearch).
- **Correlação OOTB** — `traceId` clicável em logs e métricas.
- **`/metrics`** scrape direto pelo Prometheus.

### Negativas / Trade-offs aceitos
- **5 containers** de observabilidade no compose — pesado para CI, mas profile `core` permite subir só o app sem.
- **Tempo single binary** em dev — em prod, S3-backed multi-tenant.
- **Sampler** está em `always_on` em dev; em prod, sampling head-based em ~10% (config no collector).

### Riscos e mitigações
| Risco | Probabilidade | Impacto | Mitigação |
|---|---|---|---|
| Storage Tempo/Loki cresce em dev | média | baixo | Retenção 24h em dev; `tempodata`/`lokidata` em volume nomeado |
| `correlationId` ausente em logs internos | média | alto | F7.1 verifica via lint regex; pipeline `LoggingBehavior` enriquece todo handler |
| Dashboards quebram com upgrade do Grafana | baixa | médio | JSON dashboards versionados em `infra/grafana/dashboards/` |

## Plano de revisão

- Reavaliar quando dor de cardinalidade em métricas custom aparecer (Cashflow.* labels).
- Métrica de saúde: tempo médio de scrape Prometheus < 100 ms; traces ingest rate < 1k spans/s em dev.

## Referências

- [OpenTelemetry docs](https://opentelemetry.io/docs/).
- [Grafana — Three pillars of observability](https://grafana.com/blog/2021/04/01/observability/).
- [.NET OTel auto-instrumentation](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry.Instrumentation.AspNetCore).
- ADRs relacionadas: [ADR-0007](ADR-0007-rabbitmq-vs-kafka.md), [ADR-0008](ADR-0008-masstransit-outbox.md).
