# ADR-0009: Redis 7 como cache aside com stampede lock (sem Redlock)

- **Status:** Accepted
- **Data:** 2026-05-13
- **Decisores:** @marcelo
- **Tags:** `infra`, `cache`, `redis`

## Contexto e problema

O endpoint `GET /balances/{merchantId}/daily?date=...` precisa sustentar **50 req/s × 60s com erro < 5%** (NFR-P-01). Sob carga sustentada, ≥ 95% do tráfego deve hit cache (TTL 60s); cache miss inicial não pode causar **thundering herd** no Mongo.

Critérios:

- Cache aside com TTL 60s.
- Stampede lock simples no miss (apenas 1 caller recalcula; demais aguardam e releem).
- Atômico (não pode ter race que serve dado stale por mais de 1 ciclo).
- Latência sub-ms no cache hit.

## Direcionadores da decisão

- **D1.** NFR-P-01 (MUST): 50 req/s × 60s.
- **D2.** NFR-P-02 (SHOULD): `cache_hit_p95 < 200ms`.
- **D3.** Sem complexidade desnecessária — Redlock formal exige 3+ nós Redis, e o desafio é dev local.
- **D4.** Idempotência distribuída opcional (`Idempotency-Key`) pode usar Redis no futuro (atualmente Postgres unique constraint).

## Alternativas consideradas

### Opção A — Sem cache, depender só de Mongo + índice
- **Contras:** Mongo single-node serve 50 req/s, mas com p(95) ≥ 50 ms cache miss; 60s sustentado pressiona índices; NFR atingido mas sem folga.

### Opção B — Cache em memória do processo (`IMemoryCache`)
- **Prós:** zero infra extra; latência absurda.
- **Contras:** **falha em escalabilidade horizontal** (NFR-P-03 SHOULD: Consolidation horizontalmente escalável com cache compartilhado) — múltiplas réplicas teriam caches divergentes; cache miss multiplica × N réplicas; stampede pior.

### Opção C — Redis com cache aside simples (TTL apenas) — sem stampede lock
- **Contras:** cache miss simultâneo de N callers gera N queries ao Mongo (thundering herd).

### Opção D — Redis com `SET key value NX EX 5` (stampede lock simples) — **escolhida**
- Cache aside com TTL 60s. Em miss:
  1. Tenta `SET miss_lock:<key> <uuid> NX EX 5`.
  2. Se vence, busca no Mongo e popula o cache (`SET key value EX 60`).
  3. Se perde, dorme 100 ms e relê o cache (provavelmente populado pelo vencedor).

### Opção E — Redlock (algoritmo Antirez)
- 3+ nós Redis independentes, quorum.
- **Contras:** crítica formal do Martin Kleppmann; over-engineering em dev local; benefício marginal para "stampede" (que é problema de coordenação, não de exclusão mútua estrita).

## Decisão

Escolhemos **Redis 7.4-alpine** com cache aside simples + stampede lock `SET NX EX`.

**Pseudo-código do GetDailyBalance handler:**

```csharp
var key = $"daily_balance:{merchantId}:{date:yyyy-MM-dd}";
var cached = await redis.GetStringAsync(key);
if (cached is not null) return Deserialize(cached);  // hit

// miss: try stampede lock
var lockKey = $"daily_balance_lock:{merchantId}:{date:yyyy-MM-dd}";
var won = await redis.StringSetAsync(lockKey, lockId, TimeSpan.FromSeconds(5), When.NotExists);
if (won)
{
    var fresh = await mongoRepo.GetDailyBalanceAsync(merchantId, date);
    await redis.SetStringAsync(key, Serialize(fresh), TimeSpan.FromSeconds(60));
    await redis.KeyDeleteAsync(lockKey);  // libera cedo
    return fresh;
}

// perdemos a corrida — aguarda e tenta de novo (com timeout)
await Task.Delay(100, ct);
var second = await redis.GetStringAsync(key);
if (second is not null) return Deserialize(second);

// fallback: lê direto do Mongo (proteção contra deadlock se vencedor crashou)
return await mongoRepo.GetDailyBalanceAsync(merchantId, date);
```

**Configuração Redis:**

- Imagem `redis:7.4-alpine`, `--appendonly yes` (persistence AOF).
- Persistência aceitável para cache: se cair, repopula em 60s.
- Sem cluster — single node em dev; em prod, sentinel ou cluster.

**Polly v8 `mongo-read` pipeline** envolve a chamada ao Mongo no miss (Timeout 2s + CircuitBreaker 50% em 30s com break duration 15s + Retry 3 exponencial). Registrado em `Cashflow.SharedKernel.AddCashflowResilience` via `ResiliencePipelineProvider`, consumido nos handlers `GetDailyBalanceHandler`, `GetPeriodBalanceHandler` e `GetCurrentBalanceHandler`.

## Consequências

### Positivas
- 50 req/s × 60s com 0% erro (ver `docs/performance/k6-result-2026-05-14.json`).
- Cache hit p(95) ≈ 53 ms (rede + parse JSON dominam, Redis ≤ 1ms).
- Stampede contido: 1 query ao Mongo por janela de 60s × merchant × dia.
- Latência tolerável no fallback de lock perdido (100ms + cache reread).

### Negativas / Trade-offs aceitos
- **Não é distributed lock formal** (Kleppmann "How to do distributed locking"). Aceitável porque a janela de risco é << TTL — se o vencedor crashar, o lock expira em 5s e o próximo caller que detectar o miss assume.
- **Stale read** possível por até 60s. Documentado na resposta (`cache.ageSeconds`).
- **Sem cluster** em dev — single point of failure local; em prod, sentinel.

### Riscos e mitigações
| Risco | Probabilidade | Impacto | Mitigação |
|---|---|---|---|
| Cache poisoning (valor inválido escrito) | baixa | alto | Serialização tipada via `System.Text.Json`; validação no read |
| Lock vencedor morre e ninguém recalcula | baixa | médio | TTL 5s do lock + fallback direto ao Mongo |
| Cluster Redis instável em prod | média | médio | Documentar sentinel; usar `redis:7.4` (sem cluster mode em dev) |

## Plano de revisão

- Reavaliar com Redlock se distributed locking virar requisito (ex.: rate limit cross-region).
- Métrica de saúde: cache hit rate > 95% em estado estacionário; lock contention < 0.1% das requisições.

## Referências

- Martin Kleppmann, [How to do distributed locking](https://martin.kleppmann.com/2016/02/08/how-to-do-distributed-locking.html).
- [Redis cache aside pattern](https://learn.microsoft.com/azure/architecture/patterns/cache-aside).
- ADRs relacionadas: [ADR-0006](ADR-0006-mongo-read-side.md), [ADR-0004](ADR-0004-cqrs-fisico.md).
