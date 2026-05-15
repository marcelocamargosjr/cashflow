# ADR-0005: PostgreSQL 16 como write store do Ledger

- **Status:** Accepted
- **Data:** 2026-05-13
- **Decisores:** @marcelo
- **Tags:** `dados`, `infra`, `postgres`

## Contexto e problema

O Ledger é a fonte de verdade dos lançamentos. As operações sobre ele exigem:

- **Transações ACID** para gravar a entry + a linha de Outbox + a linha de idempotência **na mesma TX**;
- **Constraint única** `(merchantId, idempotencyKey)` para garantir replay seguro;
- **SKIP LOCKED** ou equivalente para o publisher do Outbox drenar mensagens em paralelo sem disputa;
- **JSONB** para serializar o payload do evento sem perder type-safety na borda;
- **Migrations versionadas** com `Up` **e** `Down` (rollback humano viável).

## Direcionadores da decisão

- **D1.** NFR-R-02 (MUST): Outbox transacional — "ou persiste e publica, ou nenhum dos dois".
- **D2.** NFR-M-04 (SHOULD): migrations versionadas, idempotentes, com rollback.
- **D3.** Suporte first-class do MassTransit Outbox EF Core ([ADR-0008](ADR-0008-masstransit-outbox.md)).
- **D4.** Ferramental local sem licença (Docker image oficial Alpine, ≈ 80 MB).

## Alternativas consideradas

### Opção A — PostgreSQL 16 — **escolhida**
- ACID, `FOR UPDATE SKIP LOCKED` desde 9.5, JSONB, MVCC, `pg_partman` se precisar partição.
- **Prós:** maduro, FOSS, EF Core via Npgsql; migrations via `dotnet ef`; suportado por MassTransit Outbox.
- **Contras:** operação simples para 1 nó; HA em prod exige Patroni/Crunchy.

### Opção B — SQL Server (Linux)
- Mesma maturidade ACID; melhor integração com `dotnet ef` em alguns aspectos.
- **Contras:** licença em prod (Express limitado); imagem Docker pesada; MassTransit Outbox suporta mas é cidadão de 2ª classe em relação ao Postgres no ecossistema.

### Opção C — MySQL 8 / MariaDB
- ACID com InnoDB; `SKIP LOCKED` desde MySQL 8.
- **Contras:** menos investimento da comunidade .NET em provider; JSON column type menos rico que JSONB.

### Opção D — CockroachDB / YugabyteDB (Postgres wire-compat)
- Distribuição horizontal automática.
- **Contras:** over-engineering; latência maior; sem Outbox MassTransit dedicado.

## Decisão

Escolhemos **PostgreSQL 16.3-alpine** com:

- **Npgsql 9** como provider EF Core.
- **EF Core 9.0** com `EnableRetryOnFailure` (`NpgsqlRetryingExecutionStrategy`).
- **Schema separado** `messaging` para `OutboxMessage` / `OutboxState` (gerenciados pelo MassTransit) — ledger e mensageria em schemas distintos.
- **Migrations** em `Cashflow.Ledger.Infrastructure/Persistence/Migrations/` com `Up` + `Down`.

**Tabelas principais:**

| Tabela | Schema | Propósito |
|---|---|---|
| `entries` | `public` | Lançamentos confirmados/estornados |
| `idempotency_keys` | `public` | `(merchantId, key) UNIQUE` → `entryId` |
| `OutboxState` | `messaging` | Estado do dispatcher do MassTransit |
| `OutboxMessage` | `messaging` | Mensagens pendentes/em-flight |
| `__EFMigrationsHistory` | `ledger` | Histórico EF |

**Connection string** template em `infra/.env.example`. Em dev, porta host `5433` evita conflito com instalações nativas (5432 é frequentemente ocupada).

## Consequências

### Positivas
- Outbox transacional naturalmente garantido (Save em uma única `SaveChangesAsync`).
- `Idempotency-Key` enforçada por constraint — não é "convenção".
- Tooling EF Core maduro: `dotnet ef migrations add`, `database update`, `script` (geração SQL).
- Diagnóstico via `pgAdmin` (profile `tools`) e queries diretas.

### Negativas / Trade-offs aceitos
- **HA é externalizado** — em prod, replicação síncrona/standby fica fora do escopo do desafio.
- **Backup/restore** manual em dev — `pg_dump`/`pg_restore` documentados no runbook.

### Riscos e mitigações
| Risco | Probabilidade | Impacto | Mitigação |
|---|---|---|---|
| Migration acidentalmente destrutiva em prod | baixa | alto | `Down` obrigatório; revisão de PR; CI roda `dotnet ef migrations script` |
| `OutboxMessage` cresce indefinidamente | baixa | médio | Retention nativa do MassTransit (Cleanup periódico); métrica de pending |
| Constraint `idempotency_keys` virar hot spot | baixa | médio | Índice composto `(merchantId, key)`; volume estimado baixo |

## Plano de revisão

- Reavaliar se write throughput ultrapassar 1000 entries/s sustentados (precisaria sharding ou partition).
- Métrica de saúde: `pg_stat_database` (transações/s, deadlocks); `OutboxMessage` row count < 1000.

## Referências

- [PostgreSQL 16 docs](https://www.postgresql.org/docs/16/index.html).
- [MassTransit Postgres Outbox](https://masstransit.io/documentation/configuration/middleware/outbox).
- [Npgsql provider docs](https://www.npgsql.org/efcore/).
- ADRs relacionadas: [ADR-0004](ADR-0004-cqrs-fisico.md), [ADR-0008](ADR-0008-masstransit-outbox.md).
