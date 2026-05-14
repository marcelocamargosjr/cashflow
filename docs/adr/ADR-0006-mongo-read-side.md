# ADR-0006: MongoDB 7 como read store da Consolidação

- **Status:** Accepted
- **Data:** 2026-05-13
- **Decisores:** @marcelo
- **Tags:** `dados`, `infra`, `mongodb`, `projecao`

## Contexto e problema

O read side serve a query dominante:

> "Saldo consolidado do merchant X no dia Y" (`GET /balances/{merchantId}/daily?date=...`).

Outras queries são variantes: período (N dias somados), atual (hoje em `America/Sao_Paulo`). Todas projetam totais (credit/debit/balance) + breakdown por categoria + lastUpdatedAt.

Critérios:

- **Sem JOIN** — cada documento é auto-contido para responder a query em 1 leitura.
- **Upsert atômico** — o consumer precisa criar-ou-atualizar a projeção idempotentemente (mesmo evento entregue 2x não duplica).
- **Throughput de leitura** — 50 req/s sustentado (NFR-P-01), com cache aside ([ADR-0009](ADR-0009-redis-cache.md)).
- **TTL** para `processed_events` (idempotência) — drop automático após 7 dias.

## Direcionadores da decisão

- **D1.** NFR-P-01 (MUST): 50 req/s × 60s com erro < 5%.
- **D2.** NFR-R-03 (MUST): consumer idempotente por `EventId` — precisa de `$ne` filter em upsert atômico.
- **D3.** Modelo de dados: 1 documento por `(merchantId, date)` casa naturalmente com a query.
- **D4.** Operação simples via Docker; imagem oficial.

## Alternativas consideradas

### Opção A — MongoDB 7 — **escolhida**
- Schema flexível; `findOneAndUpdate` atômico com `$ne` + `$inc`; índice TTL nativo; agregações poderosas se necessário no futuro.
- **Prós:** documento por dia casa perfeitamente; idempotência via `$ne EventId` é trivial; TTL automático para `processed_events`.
- **Contras:** transações multi-document em prod exigem replica set; perda de constraint cross-collection.

### Opção B — Postgres com tabela `daily_balance` + view materializada
- Manter no mesmo store do Ledger.
- **Contras:** falha em **NFR-A-01** ([ADR-0004](ADR-0004-cqrs-fisico.md)) — write/read no mesmo store; refresh de MV agenda manutenção; sem TTL nativo.

### Opção C — Redis como read store primário
- Hash por `(merchantId, date)`.
- **Prós:** latência sub-ms.
- **Contras:** sem TTL fine-grained por documento sem custos extras; sem queries por intervalo; durabilidade RDB/AOF é melhor mas ainda não é DB primário; sem agregação para period.

### Opção D — ElasticSearch
- Full-text + agregações poderosas.
- **Contras:** over-engineering; recursos demais para uma projeção de 4 números + categoria.

## Decisão

Escolhemos **MongoDB 7.0.12** com:

- **Database:** `cashflow_consolidation`.
- **Coleção `daily_balance`:**
  - Índice único composto `{ merchantId: 1, date: 1 }`.
  - Documentos: `{ merchantId, date, totalCredits, totalDebits, balance, entriesCount, byCategory[], lastUpdatedAt, revision }`.
- **Coleção `processed_events`:**
  - Índice único em `{ eventId: 1 }`.
  - Índice TTL em `{ processedAt: 1 }` com `expireAfterSeconds: 604800` (7 dias).
- **`init.js`** mountado em `/docker-entrypoint-initdb.d/init.js` para criar usuário de aplicação, índices e coleções no primeiro start.
- **Driver:** `MongoDB.Driver` 3.x.
- **Idempotência do consumer:**

```csharp
await collection.FindOneAndUpdateAsync(
    Builders<DailyBalanceDoc>.Filter.And(
        Builders<DailyBalanceDoc>.Filter.Eq(d => d.MerchantId, evt.MerchantId),
        Builders<DailyBalanceDoc>.Filter.Eq(d => d.Date, evt.EntryDate),
        Builders<DailyBalanceDoc>.Filter.Ne(d => d.LastEventId, evt.EventId)
    ),
    Builders<DailyBalanceDoc>.Update
        .Inc(d => d.TotalCredits, evt.Type == "Credit" ? evt.Amount : 0)
        .Inc(d => d.TotalDebits,  evt.Type == "Debit"  ? evt.Amount : 0)
        .Inc(d => d.EntriesCount, 1)
        .Set(d => d.LastEventId, evt.EventId)
        .Set(d => d.LastUpdatedAt, _clock.UtcNow),
    new FindOneAndUpdateOptions<DailyBalanceDoc> { IsUpsert = true });
```

O filtro `$ne EventId` faz com que reentregas do mesmo evento sejam **no-op** (FindOneAndUpdate retorna `null`).

Porta host **27018** evita conflito com `mongod` nativo no Windows (lição registrada nas memórias do projeto).

## Consequências

### Positivas
- Query `/balances/{merchantId}/daily` é **1 leitura indexada** + cache (≤ 1ms cache hit; ≤ 50 ms cache miss).
- Idempotência via `$ne` é atômica — não precisa lock distribuído nem coordenação.
- TTL nativo elimina rotina manual de limpeza.
- Documento por dia escala lateralmente (sharding por `merchantId` no futuro).

### Negativas / Trade-offs aceitos
- **Sem transações cross-collection** entre `daily_balance` e `processed_events` em standalone (Mongo single-replica). Aceitável porque `processed_events` é apenas auditoria — a idempotência real está no filtro do upsert.
- **Schema evolution** sem migrations formais — cuidado com `Revision` field e versionamento de eventos ([ADR-0007](ADR-0007-rabbitmq-vs-kafka.md)).

### Riscos e mitigações
| Risco | Probabilidade | Impacto | Mitigação |
|---|---|---|---|
| Índice composto não usado por query | baixa | alto | Test de integração valida `explain()` plan |
| TTL backlog (Mongo 7 default 60s monitor) | baixa | baixo | Aceitável — limpeza de auditoria pode atrasar |
| Drift de schema entre eventos e doc | média | alto | Versionamento de evento + handler por versão |

## Plano de revisão

- Reavaliar se documento médio passar de 16 MB (improvável para nossa cardinalidade).
- Reavaliar se cardinalidade de merchants × dias justificar sharding (hoje 1 nó basta).
- Métrica de saúde: `mongo.collection.daily_balance.size` + `latency_ms p95 < 50` em queries diretas.

## Referências

- [MongoDB 7 docs](https://www.mongodb.com/docs/v7.0/).
- [findOneAndUpdate atomicity](https://www.mongodb.com/docs/manual/reference/method/db.collection.findOneAndUpdate/).
- [TTL indexes](https://www.mongodb.com/docs/manual/core/index-ttl/).
- ADRs relacionadas: [ADR-0004](ADR-0004-cqrs-fisico.md), [ADR-0009](ADR-0009-redis-cache.md).
