# ADR-0004: CQRS físico (write Postgres / read Mongo) em vez de CQRS lógico no mesmo store

- **Status:** Accepted
- **Data:** 2026-05-13
- **Decisores:** @marcelo
- **Tags:** `arquitetura`, `cqrs`, `dados`

## Contexto e problema

O desafio define dois serviços (Lançamentos e Consolidado Diário) com requisitos divergentes:

- **Write side (Ledger):** transações ACID com invariantes (`Idempotency-Key`, `EntryStatus` state machine, Outbox em mesma TX) — precisa de RDBMS.
- **Read side (Consolidation):** consulta dominante é "saldo do merchant X no dia Y" — documento agregado por `(merchantId, date)` com totais e breakdown por categoria; sem JOIN, com cache de 60s.

Precisamos decidir se separamos fisicamente (DBs diferentes) ou logicamente (mesmo store, schemas/coleções diferentes).

## Direcionadores da decisão

- **D1.** NFR-A-01 (MUST): Ledger sobrevive a Consolidation indisponível — pede componentes independentes operacionalmente.
- **D2.** NFR-P-01 (MUST): Consolidation sustenta 50 req/s — pede otimização específica do read.
- **D3.** Demonstrar empiricamente CQRS no sentido de "modelos distintos para write e read" (não apenas separação de namespaces).
- **D4.** Operação aceitável via `docker compose` — sem explosão de containers.

## Alternativas consideradas

### Opção A — CQRS lógico no mesmo Postgres
- Tabela `entries` (write) + materialized view `daily_balance` (read) refresh por trigger ou worker.
- **Prós:** 1 DB, 1 backup, transações cruzando os dois lados.
- **Contras:** **falha em D1** — se Postgres cai, ambos os lados caem; **falha em D2** — materialized view + 50 req/s sem cache exige mais cuidado de tuning (índices, autovacuum) do que uma projeção Mongo por documento.

### Opção B — CQRS físico (Postgres write + Mongo read) — **escolhida**
- Ledger persiste no Postgres, publica evento `EntryRegisteredV1` via Outbox, Worker consome e atualiza `daily_balance` no Mongo.
- **Prós:** isolamento real (D1); read otimizado por documento, com cache (D2); demonstra CQRS de verdade (D3).
- **Contras:** dois stores para operar; eventual consistency (lag de projeção).

### Opção C — Event Sourcing puro (Marten/EventStoreDB)
- Stream de eventos como fonte de verdade; projeções derivadas (read models).
- **Prós:** auditabilidade total; replay grátis.
- **Contras:** maior curva; ferramental adicional; over-engineering para o desafio.

### Opção D — CQRS lógico no Mongo (write + read no mesmo cluster)
- Mongo cluster com replicas separadas para read e write.
- **Contras:** Mongo single-document transactions não trazem o conforto do Postgres para invariantes do write; **falha em D1** (mesmo cluster).

## Decisão

Escolhemos a **Opção B — CQRS físico** com:

- **Postgres 16** para o write side (transações ACID + EF Core + Outbox MassTransit em mesma TX — ver [ADR-0008](ADR-0008-masstransit-outbox.md)).
- **MongoDB 7** para o read side (1 documento por `(merchantId, date)`, índice único composto, índice TTL em `processed_events` para idempotência — ver [ADR-0006](ADR-0006-mongo-read-side.md)).
- **Redis** como cache aside ([ADR-0009](ADR-0009-redis-cache.md)).
- **RabbitMQ** como veículo dos eventos ([ADR-0007](ADR-0007-rabbitmq-vs-kafka.md)).

**Eventual consistency** é aceita explicitamente — lag SLO < 5s (P95). Quando Consolidation cai, o backlog se acumula no Rabbit; quando volta, é drenado em segundos (chaos validate confirma catch-up < 60s para 100 events).

## Consequências

### Positivas
- **NFR-A-01** demonstrado em `make chaos-validate` (100/100 entries criadas com Mongo+Worker offline).
- Read side otimizado pelo modelo de dados (sem JOIN, sem GROUP BY ad-hoc).
- Escalabilidade independente: pode-se subir N réplicas do Consolidation.Api sem mexer no Ledger.
- Cache aside trivial no read (TTL 60s).

### Negativas / Trade-offs aceitos
- **Dois stores** para operar, com backup/disaster recovery distintos.
- **Eventual consistency** — UI deve mostrar "última atualização X" e tolerar refresh.
- **Reverter** vira evento (`EntryReversedV1`) com snapshot — não é rollback transacional cross-DB.

### Riscos e mitigações
| Risco | Probabilidade | Impacto | Mitigação |
|---|---|---|---|
| Drift de schema entre evento e projeção | média | alto | Versionamento do evento (`.v1`); JSON schema em `docs/openapi/events/` |
| Lag de projeção crescer silenciosamente | média | médio | Métrica `cashflow.consolidation.lag_seconds` + alerta SLO < 5s |
| Outbox encher se Rabbit cair | baixa | alto | Métrica `cashflow.outbox.pending` + alerta > 1000 |
| Reverter sem snapshot quebra catch-up | baixa | alto | `EntryReversedV1` carrega snapshot (ver contract) — sem chamada síncrona ao Ledger |

## Plano de revisão

- Reavaliar se a relação write/read inverter (atualmente 1:N — read domina).
- Métrica de saúde: lag P95 < 5s; outbox pending < 100 em estado estacionário.

## Referências

- Greg Young, [CQRS Documents](https://cqrs.files.wordpress.com/2010/11/cqrs_documents.pdf).
- Martin Fowler, [CQRS](https://martinfowler.com/bliki/CQRS.html).
- Pat Helland, [Data on the Outside vs Data on the Inside](https://queue.acm.org/detail.cfm?id=3415014).
- ADRs relacionadas: [ADR-0005](ADR-0005-postgres-write-side.md), [ADR-0006](ADR-0006-mongo-read-side.md), [ADR-0008](ADR-0008-masstransit-outbox.md), [ADR-0009](ADR-0009-redis-cache.md).
