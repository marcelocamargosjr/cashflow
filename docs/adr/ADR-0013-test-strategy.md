# ADR-0013: Estratégia de testes — pirâmide com unit + integration + architecture + k6

- **Status:** Accepted
- **Data:** 2026-05-13
- **Decisores:** @marcelo
- **Tags:** `testes`, `qualidade`, `cobertura`, `netarchtest`, `testcontainers`, `k6`

## Contexto e problema

A Definition of Done do projeto exige:

- `dotnet test` verde, **0 warnings**.
- Cobertura **Domain ≥ 90%** (line) e **Application ≥ 80%**.
- Architecture tests (NetArchTest) verdes.
- NFR provados em k6 (`http_req_failed: rate<0.05` em 50 req/s × 60s).
- Cenários de integração end-to-end com Postgres, Mongo, Rabbit, Redis reais — sem mocks de infraestrutura.

Precisamos decidir como dividir o esforço de teste sem que a pirâmide vire um diamante (muito integration test caro) nem uma ampulheta (muito unit test mockado que não pega bugs reais).

## Direcionadores da decisão

- **D1.** Feedback rápido em PR: unit tests rodam em < 30s, integration em < 5min.
- **D2.** Cobertura **real** de invariantes de domínio (Money, Entry state machine).
- **D3.** Architecture invariants enforced em CI (Clean Arch [ADR-0003](ADR-0003-clean-architecture.md)).
- **D4.** Integration tests rodam em qualquer máquina com Docker — sem state compartilhado.
- **D5.** NFR comprovado por dados, não declaração.

## Alternativas consideradas

### Opção A — Apenas unit tests com mocks pesados
- **Contras:** alta cobertura ilusória; bugs reais (constraints SQL, idempotência) passam.

### Opção B — Pirâmide canônica unit > integration > E2E — **escolhida**
- Maioria unit com Domain puro; integration tests usando Testcontainers; sem E2E pesado (k6 cobre carga).
- **Prós:** rápido em PR; cobertura significativa; integration tests provam happy + erro reais.
- **Contras:** Testcontainers exige Docker no CI runner.

### Opção C — E2E heavy (Playwright + frontend + APIs reais)
- **Contras:** flaky; lento; frontend é MAY no escopo.

## Decisão

Estrutura de testes:

### 1. Unit tests — `tests/Cashflow.*.UnitTests`

- **Frameworks:** xUnit + FluentAssertions + NSubstitute + Bogus.
- **Foco:** Domain (Money, Entry invariantes, EntryStatus state machine) e Application (handlers de command/query — validators, pipeline behaviors, mediator handlers com repository mockado).
- **Cobertura alvo:** Domain ≥ 90%, Application ≥ 80%.
- **Convenção de nome:** `Method_Scenario_ExpectedResult` (ex.: `RegisterEntry_DuplicateIdempotencyKey_ReturnsExistingEntry`).
- **Sem mocks no Domain** — só objetos reais (`Money.Brl(150m)`).
- **Builders:** `EntryBuilder` (testdata) para reduzir setup duplicado.

### 2. Integration tests — `tests/Cashflow.*.IntegrationTests`

- **Frameworks:** xUnit + WebApplicationFactory + Testcontainers (.NET).
- **Cenários (IT-01..08, definidos no plano de testes do projeto):**
  - IT-01 Register entry happy path (Postgres + Outbox + RabbitMQ via Testcontainer).
  - IT-02 Idempotency replay → mesmo recurso.
  - IT-03 Reverse propaga e atualiza projection.
  - IT-04 Consumer idempotente — replay do mesmo `EventId` não duplica.
  - IT-05 Auth: 401 sem JWT; 403 com merchantId diferente.
  - IT-06 Rate-limit: 429 ao explodir `entry-write-policy`.
  - IT-07 Health endpoints retornam status correto sob dependência indisponível.
  - IT-08 Stampede lock: 100 callers simultâneos no miss → 1 query ao Mongo.
- **Fixture:** WebApplicationFactory custom (`CashflowWebAppFactory`) que exporta env vars antes do `Program.Main` (memória do projeto: WAF + env-var bootstrap pattern).
- **Limpeza:** containers efêmeros por fixture; sem state shared entre testes.

### 3. Architecture tests — `tests/Cashflow.ArchitectureTests`

- **Framework:** NetArchTest.Rules.
- **Regras:**
  - `Cashflow.*.Domain` não referencia `EntityFrameworkCore`, `MediatR`, `MassTransit`, `Microsoft.AspNetCore.*`.
  - `Cashflow.*.Application` não referencia `Cashflow.*.Infrastructure`.
  - Handlers MediatR são `internal sealed`.
  - Validators FluentValidation no mesmo namespace do command/query.
  - Endpoints estão em extensions `Map<Feature>Endpoints`.
  - DTOs são `record` (não `class`).

### 4. Performance tests — `perf/k6`

- **Cenários:**
  - `balance-50rps.js` — NFR literal (50 req/s × 60s, thresholds `http_req_failed: rate<0.05` + `http_req_duration: p(95)<500`).
  - `balance-smoke.js` — 1 req/s × 30s para validar pipeline antes do run NFR.
- **Run:** `make perf` (via `scripts/make-perf.sh`) salva output em `docs/performance/k6-result-YYYY-MM-DD.json` como evidência.

### 5. Chaos test (NFR-A-01)

- **Script:** `scripts/chaos-validate.sh` — derruba `mongo` + `consolidation-*`, dispara 100 POSTs, espera catch-up < 90s. Saída na seção 12 do README.

### Convenções gerais

- `ITestOutputHelper` em vez de `Console.WriteLine`.
- Nenhum mock em integration test (Testcontainers reais).
- `Cashflow.UnitTests.Common` para builders + fakes compartilhados.
- Cobertura coletada por XPlat Code Coverage → ReportGenerator → gate em CI.

## Consequências

### Positivas
- **Pirâmide bem-balanceada** — unit < 30s, integration ~3 min, NFR sob demanda.
- **Architecture tests** bloqueiam regressão de layering em PR.
- **k6 evidência** vira artefato em `docs/performance/` — auditável.
- **NFR-A-01** demonstrado em script (`make chaos-validate`).

### Negativas / Trade-offs aceitos
- **Testcontainers exige Docker** em CI (resolvido com `docker-in-docker` ou self-hosted runner).
- **Tempo de PR** ~5 min para suite completa.
- **Flakiness** Testcontainers em ambientes com pouca RAM — mitigado com `--cpus` / start_period.

### Riscos e mitigações
| Risco | Probabilidade | Impacto | Mitigação |
|---|---|---|---|
| Coverage gate cai por refactor que move código | média | médio | Filtros por assembly em `report-generator`; PR review |
| Integration test flaky por Rabbit não-pronto | média | alto | `AwaitConsumed<T>` do MassTransit TestHarness; healthcheck wait |
| k6 falso positivo por host loaded | baixa | médio | Run em CI dedicado / runner local idle |

## Plano de revisão

- Reavaliar adicionar **Stryker.NET** (mutation testing) quando coverage estabilizar (F11).
- Métrica de saúde: PR build time < 6 min; flakiness rate < 1%.

## Referências

- Martin Fowler, [The Practical Test Pyramid](https://martinfowler.com/articles/practical-test-pyramid.html).
- [NetArchTest](https://github.com/BenMorris/NetArchTest).
- [Testcontainers .NET](https://dotnet.testcontainers.org/).
- [k6 docs](https://k6.io/docs/).
- ADRs relacionadas: [ADR-0003](ADR-0003-clean-architecture.md), [ADR-0010](ADR-0010-otel-observability.md).
