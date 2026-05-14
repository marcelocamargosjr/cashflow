# ADR-0007: RabbitMQ + MassTransit em vez de Apache Kafka

- **Status:** Accepted
- **Data:** 2026-05-13
- **Decisores:** @marcelo
- **Tags:** `infra`, `mensageria`, `rabbitmq`, `kafka`

## Contexto e problema

Comunicação Ledger → Consolidation é assíncrona (NFR-R-01). Precisamos de um broker que ofereça:

- **Entrega durável** dos eventos `EntryRegisteredV1` / `EntryReversedV1`.
- **Retry exponencial** + **DLQ** para mensagens venenosas.
- **Suporte de primeira classe no .NET** (MassTransit ou wrapper equivalente).
- **Operação local** simples (1 container) e profundidade pública (Management UI).

Volume estimado: < 1000 events/s sustentado por merchant; < 10k events/s em pico agregado.

## Direcionadores da decisão

- **D1.** NFR-R-01 (MUST): comunicação assíncrona.
- **D2.** NFR-R-02 (MUST): Outbox transacional do lado do produtor — exige cliente que se integre com EF Core (`AddEntityFrameworkOutbox`).
- **D3.** NFR-R-04 (SHOULD): retry + DLQ nativos no consumer.
- **D4.** Operação simples: 1 container, healthcheck `rabbitmq-diagnostics -q ping`, Management UI em `:15672`.
- **D5.** Familiaridade do autor (não é argumento técnico mas é real).

## Alternativas consideradas

### Opção A — RabbitMQ 3.13 + MassTransit — **escolhida**
- Broker AMQP 0.9.1, fila durável, DLQ nativo via `x-dead-letter-exchange`.
- **Prós:** DX excelente em .NET; **Outbox EF Core nativo** ([ADR-0008](ADR-0008-massimo-transit-outbox.md)); retry/circuit-breaker no consumer (`UseMessageRetry`, `UseCircuitBreaker`); Management UI; healthcheck simples.
- **Contras:** sem replay nativo (mensagem consumida some); throughput máximo menor que Kafka para mesma máquina.

### Opção B — Apache Kafka 3.x + Confluent.Kafka
- Log distribuído, replay via consumer offset.
- **Prós:** alto throughput; replay; partitioning; ordering por partição.
- **Contras:** **op pesada** (ZooKeeper/KRaft, brokers, schema registry); MassTransit Outbox Kafka existe mas é menos polido; overkill para 1k events/s; latência maior em batch defaults.

### Opção C — Azure Service Bus / SNS+SQS
- Managed brokers.
- **Contras:** **D4 viola** ("tudo via docker compose"); custo financeiro em prod sem ganho para o desafio.

### Opção D — NATS / Redis Streams
- Brokers leves.
- **Contras:** ecossistema MassTransit menor; menos garantias de durabilidade out-of-the-box.

## Decisão

Escolhemos **RabbitMQ 3.13-management-alpine** + **MassTransit 8.x**.

**Topologia:**

- **Exchange:** `Cashflow.Contracts.V1:EntryRegisteredV1` (e `EntryReversedV1`) — tipos fanout/topic gerenciados pelo MassTransit.
- **Queue por consumer:** `cashflow-consolidation-entry-registered` + DLQ `_skipped` e retry queues automáticos (`UseMessageRetry`).
- **Definições importadas via `infra/rabbitmq/definitions.json`** no startup (idempotente).
- **Versionamento de evento:** `EntryRegisteredV1` (namespace `Cashflow.Contracts.V1`) — uma versão nova vira `V2` em paralelo, sem breaking change.

**Retry policy padrão:**

```csharp
cfg.UseMessageRetry(r => r.Exponential(
    retryLimit: 5,
    minInterval: TimeSpan.FromSeconds(1),
    maxInterval: TimeSpan.FromSeconds(30),
    intervalDelta: TimeSpan.FromSeconds(2)));
```

Após 5 tentativas, vai para `_skipped` (DLQ). Runbook ([`docs/runbook.md`](../runbook.md) §2) cobre inspeção e replay.

## Consequências

### Positivas
- **Outbox EF Core nativo** elimina código manual de plumbing.
- **DLQ + retry** out-of-the-box via `UseMessageRetry`.
- **Management UI** (`http://localhost:15672`) para diagnóstico (fila, message count, rates).
- **Polling-free** (push), latência baixa para nosso volume.
- **Idempotência no consumer** desacoplada — usamos `processed_events` no Mongo ([ADR-0006](ADR-0006-mongo-read-side.md)).

### Negativas / Trade-offs aceitos
- **Sem replay nativo** — uma mensagem consumida sai da fila. Para replay, precisamos do endpoint admin `/admin/reproject` (evolução, esqueleto pronto).
- **Single-broker** em dev — em prod, cluster com mirroring/quorum queues.
- **AMQP 0.9.1** não é AMQP 1.0 — interop limitada com brokers não-AMQP-0.9.

### Riscos e mitigações
| Risco | Probabilidade | Impacto | Mitigação |
|---|---|---|---|
| Backlog na fila se consumer cair | média | médio | Métrica `cashflow.outbox.pending` + alerta; chaos validate prova catch-up < 60s |
| Mensagem venenosa parar consumer | baixa | alto | DLQ `_skipped` + alerta runbook |
| Drift de contrato entre produtor e consumer | média | alto | Versionamento (`.V1`); JSON Schema em `docs/openapi/events/` |
| Perda de mensagem se Rabbit cair sem persist | baixa | alto | `durable: true` + `delivery_mode: 2` (persistent) — defaults do MassTransit |

## Plano de revisão

- Reavaliar se throughput ultrapassar 5k events/s sustentado (provavelmente migrar para Kafka).
- Métrica de saúde: lag P95 < 5s; DLQ size < 10.

## Referências

- [RabbitMQ 3.13 docs](https://www.rabbitmq.com/docs).
- [MassTransit RabbitMQ transport](https://masstransit.io/documentation/transports/rabbitmq).
- [Confluent — When NOT to use Kafka](https://www.confluent.io/blog/dont-use-apache-kafka-consumer-groups/).
- ADRs relacionadas: [ADR-0001](ADR-0001-microsservicos-event-driven.md), [ADR-0008](ADR-0008-massimo-transit-outbox.md).
