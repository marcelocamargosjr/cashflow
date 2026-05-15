# ADR-0008: MassTransit `EntityFrameworkOutbox` em vez de Outbox manual

- **Status:** Accepted
- **Data:** 2026-05-13
- **Decisores:** @marcelo
- **Tags:** `mensageria`, `outbox`, `consistencia`

## Contexto e problema

O Outbox Pattern é obrigatório (NFR-R-02) para garantir "ou persiste e publica, ou nenhum dos dois". A implementação envolve:

1. Gravar a mensagem em uma tabela `OutboxMessage` na **mesma TX** da escrita de negócio.
2. Um dispatcher polleia a tabela com `SELECT ... FOR UPDATE SKIP LOCKED`, publica no broker e marca como processada.
3. Tratar reentregas (idempotência do consumer).
4. Limpar mensagens antigas (cleanup).

Cada passo é um lugar onde bugs sutis aparecem (especialmente em (1) com EF — interceptors, savepoints — e (2) com SKIP LOCKED e timeouts).

## Direcionadores da decisão

- **D1.** NFR-R-02 (MUST): Outbox transacional.
- **D2.** F7.1 §Anti-patterns: zero código que reimplementa biblioteca testada.
- **D3.** Tempo limitado: 1 dispatcher próprio é facilmente 200 LOC + testes; não agrega valor.
- **D4.** Compatibilidade com EF Core + Postgres ([ADR-0005](ADR-0005-postgres-write-side.md)).

## Alternativas consideradas

### Opção A — Outbox manual
- Tabela `outbox_messages`, polling worker próprio, EF SaveChanges + insert na mesma TX.
- **Prós:** controle absoluto.
- **Contras:** todo o detalhe (SKIP LOCKED, batching, jitter no polling, cleanup, retry de publish) é nosso para escrever e testar; bugs prováveis.

### Opção B — MassTransit `EntityFrameworkOutbox` — **escolhida**
- `AddEntityFrameworkOutbox<LedgerDbContext>(o => { o.UsePostgres(); o.UseBusOutbox(); })`.
- **Prós:** dispatcher gerenciado; SKIP LOCKED nativo; integração com `DbContextSaveChanges`; tabelas geradas via `dotnet ef migrations add Outbox` apontando para context do MassTransit.
- **Contras:** caixa-preta para alguns parâmetros de tuning; ciclo no DI na inicialização exige cuidado (resolver `IPublishEndpoint` na primeira `SaveChangesAsync` enquanto o bus ainda está bootando — workaround em `Program.cs` do Ledger).

### Opção C — DotNetCore.CAP
- Outbox de outro ecossistema (.NET).
- **Prós:** suporta múltiplos brokers; comunidade.
- **Contras:** menos integrado ao MassTransit; duplicaria abstrações para retry/circuit breaker que já temos.

### Opção D — Debezium + outbox table
- CDC do Postgres → Kafka.
- **Contras:** novo componente; Kafka exigência ([ADR-0007](ADR-0007-rabbitmq-vs-kafka.md) descarta).

## Decisão

Escolhemos **MassTransit `EntityFrameworkOutbox`** com configuração:

```csharp
services.AddMassTransit(x =>
{
    x.AddEntityFrameworkOutbox<LedgerDbContext>(o =>
    {
        o.QueryDelay = TimeSpan.FromSeconds(1);
        o.UsePostgres();
        o.UseBusOutbox();
    });
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(Environment.GetEnvironmentVariable("RabbitMq__Host"), "/", h =>
        {
            h.Username(Environment.GetEnvironmentVariable("RabbitMq__Username"));
            h.Password(Environment.GetEnvironmentVariable("RabbitMq__Password"));
        });
        // NOTA: `cfg.ConfigureEndpoints(ctx)` é deliberadamente OMITIDO no Ledger —
        // o Ledger apenas publica (Outbox dispatcher) e não consome nada.
        // Configurar endpoints aqui criaria receive endpoints inúteis. Veja
        // src/Cashflow.Ledger/.../MessagingServiceCollectionExtensions.cs.
    });
});

// Defesa adicional contra o cleanup automático do Inbox (que não usamos no Ledger):
// o `InboxCleanupService` é removido por reflection do contêiner após o bootstrap
// — comentário inline em MessagingServiceCollectionExtensions explica o motivo.
```

**Apenas Outbox no Ledger; Inbox no Consumer fica via `processed_events` no Mongo** (escolha explícita — não usar **os dois**: `MassTransit.Inbox` + tabela própria).

Razão: o consumer está em outro store (Mongo). Manter o `Inbox` do MassTransit no Postgres exigiria que o consumer escrevesse no Postgres em rotação, criando acoplamento desnecessário. A idempotência real é em duas camadas: (1) fast-path em `processed_events` com `Find + InsertOne` + tratamento de `DuplicateKey`; (2) guard `$ne LastAppliedEventId` em `daily_balances` via `UpdateOneAsync` em duas passadas — ver [ADR-0006](ADR-0006-mongo-read-side.md).

**Migrations:** EF migration `0002_AddOutbox` cria `messaging.OutboxState`, `messaging.OutboxMessage` (e os índices necessários).

## Consequências

### Positivas
- **Zero código** de dispatcher próprio.
- `SaveChangesAsync` da entry **e** insert do OutboxMessage atômicos (mesmo `DbContext`).
- Métrica `cashflow.outbox.pending` expõe row count para alerta SLO.
- Tooling MassTransit (logs estruturados, sondas) cobre observabilidade.

### Negativas / Trade-offs aceitos
- **Caixa-preta parcial** — afinar `QueryDelay`, `LockTimeout`, `MessageDeliveryLimit` exige conhecer a lib.
- **Ciclo de DI** na primeira inicialização (BusOutbox usa `IPublishEndpoint` enquanto bus boota). Documentado em `Program.cs` do Ledger: migrations são aplicadas com `DbContext` instanciado manualmente, com `IServiceProvider` vazio, para evitar resolver `IPublishEndpoint` durante o `MigrateAsync`.
- **Versão do MassTransit** sob CPM — atualizar exige test de regressão completo.

### Riscos e mitigações
| Risco | Probabilidade | Impacto | Mitigação |
|---|---|---|---|
| Bug em release nova do MassTransit afeta dispatcher | baixa | alto | CPM fixa versão; dependabot abre PR; CI roda integration tests |
| `OutboxMessage` cresce se broker indisponível | média | médio | Métrica + alerta; chaos restore drena em segundos |
| Replay manual difícil sem replay nativo | média | médio | Endpoint admin `/admin/reproject` planejado; runbook documentado |

## Plano de revisão

- Reavaliar se latência write → publish ultrapassar SLO 5s P95.
- Reavaliar quando o MassTransit publicar major version (8 → 9) — exige read do migration guide.
- Métrica de saúde: `OutboxMessage` row count em estado estacionário < 100; latência de publish < 1s.

## Referências

- [Pat Helland — Life Beyond Distributed Transactions](https://www.ics.uci.edu/~cs223/papers/cidr07p15.pdf).
- [MassTransit — Transactional Outbox](https://masstransit.io/documentation/configuration/middleware/outbox).
- [microservices.io — Transactional Outbox](https://microservices.io/patterns/data/transactional-outbox.html).
- ADRs relacionadas: [ADR-0005](ADR-0005-postgres-write-side.md), [ADR-0007](ADR-0007-rabbitmq-vs-kafka.md).
