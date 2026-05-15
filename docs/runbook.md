# Runbook — Cashflow

> Procedimentos operacionais para cenários comuns: **lag de projeção**, **inspeção de DLQ**, **replay de eventos**, **rotação de secrets**, **resposta a SLO breach**. Comandos são copiáveis (PowerShell ou Bash) e assumem a stack de pé via `make up`.

---

## 1. Diagnóstico de lag de projeção

**Sintoma:** `GET /balances/{merchantId}/daily?date=...` retorna `lastUpdatedAt` antigo apesar de POSTs recentes terem 201; ou alerta SLO "lag projeção P95 > 30s por 5 min".

### 1.1 Localizar a fonte do lag (4 saltos possíveis)

```bash
# 1) Outbox no Postgres — mensagens pendentes (não publicadas)
docker exec -it cashflow-postgres \
  psql -U cashflow -d cashflow_ledger -c \
  "SELECT COUNT(*) AS pending FROM messaging.\"OutboxMessage\" WHERE \"DeliveredOn\" IS NULL;"

# 2) Fila do RabbitMQ — mensagens enfileiradas aguardando consumer
curl -fsS -u cashflow:CHANGE_ME_IN_PROD \
  http://localhost:15672/api/queues/%2F | \
  jq '.[] | select(.name | startswith("cashflow-")) | {queue: .name, ready: .messages_ready, unacked: .messages_unacknowledged}'

# 3) Consumer rate (logs do worker)
docker logs --tail=200 cashflow-consolidation-worker | \
  grep -E "Consumed|EntryRegistered|EntryReversed"

# 4) Mongo — última revision aplicada
docker exec -it cashflow-mongo mongosh -u cashflow -p CHANGE_ME_IN_PROD \
  --authenticationDatabase admin --eval \
  "db.getSiblingDB('cashflow_consolidation').daily_balances.find({}, {merchantId:1, date:1, revision:1, lastUpdatedAt:1}).sort({lastUpdatedAt:-1}).limit(5).pretty()"
```

| Onde está o gargalo | Diagnóstico | Ação |
|---|---|---|
| `OutboxMessage.pending` > 1000 | Dispatcher do MassTransit não dá conta | Escalar `ledger-api` (mais réplicas); investigar lock contention; checar latência do Rabbit |
| `messages_ready` cresce no Rabbit | Consumer offline ou lento | Ver §1.2; checar `cashflow-consolidation-worker` |
| Worker logs sem `Consumed` recente | Consumer em retry/CB | Ver §2 (DLQ) |
| Mongo `revision` antiga | Worker consumindo mas projection lenta | Profilar `updateOne` (Pass1/Pass2 em `daily_balances`); verificar índice composto `ix_merchant_date` (`{merchantId:1, date:-1}`) |

### 1.2 Verificar saúde do consumer

```bash
docker compose -f infra/docker-compose.yml --profile app ps consolidation-worker
docker stats --no-stream cashflow-consolidation-worker
docker exec -it cashflow-rabbitmq rabbitmqctl list_consumers
```

Métrica Prometheus relevante: `cashflow_consolidation_lag_seconds` (custom). Dashboard `NFR Validation` no Grafana plota a curva.

---

## 2. Inspeção de DLQ (`_skipped`)

Mensagens venenosas (que falham retry exponencial 5x) vão para a fila `_skipped` correspondente.

### 2.1 Listar DLQs com mensagens

```bash
curl -fsS -u cashflow:CHANGE_ME_IN_PROD \
  http://localhost:15672/api/queues/%2F | \
  jq '.[] | select(.name | endswith("_skipped")) | {queue: .name, ready: .messages_ready}'
```

### 2.2 Espiar conteúdo de uma DLQ (sem consumir)

```bash
# Endpoint /api/queues/{vhost}/{name}/get foi removido — use o shovel ou copia manual
docker exec -it cashflow-rabbitmq rabbitmqadmin --vhost=/ \
  --username=cashflow --password=CHANGE_ME_IN_PROD \
  get queue=cashflow-consolidation-entry-registered_skipped requeue=true count=5
```

`requeue=true` significa "olha mas devolve" — não remove a mensagem.

### 2.3 Inspecionar headers de falha

A header `MT-Fault-StackTrace` (e `MT-Fault-ExceptionType`) carrega o motivo da falha:

```bash
docker exec -it cashflow-rabbitmq rabbitmqadmin --vhost=/ \
  --username=cashflow --password=CHANGE_ME_IN_PROD \
  get queue=cashflow-consolidation-entry-registered_skipped count=1 ackmode=ack_requeue_true | \
  jq -r '.[0].properties.headers["MT-Fault-StackTrace"]'
```

---

## 3. Replay de eventos da DLQ

Cenário: motivo da falha foi corrigido (bug no consumer, schema field opcional ausente, etc.) e queremos reprocessar.

### 3.1 Replay simples — shovel da DLQ para a fila principal

```bash
# Liga o shovel plugin (já habilitado em rabbitmq/enabled_plugins)
docker exec -it cashflow-rabbitmq rabbitmqctl set_parameter shovel replay-dlq \
  '{"src-protocol":"amqp091","src-uri":"amqp://localhost","src-queue":"cashflow-consolidation-entry-registered_skipped","dest-protocol":"amqp091","dest-uri":"amqp://localhost","dest-queue":"cashflow-consolidation-entry-registered","ack-mode":"on-confirm"}'

# Aguarde drenar
watch -n 1 'curl -sSu cashflow:CHANGE_ME_IN_PROD http://localhost:15672/api/queues/%2F/cashflow-consolidation-entry-registered_skipped | jq .messages_ready'

# Remova o shovel quando terminar
docker exec -it cashflow-rabbitmq rabbitmqctl clear_parameter shovel replay-dlq
```

> O consumer é idempotente em duas camadas: fast-path em `processed_events` (Find + Insert) e guard `$ne LastAppliedEventId` em `daily_balances` (UpdateOneAsync). Replays não duplicam.

### 3.2 Replay total da projeção (rebuild)

Esqueleto de endpoint `POST /admin/reproject?from=&to=` está planejado (evolução documentada no README). Manualmente:

```bash
# Truncar a projeção do merchant + reprocessar a partir do snapshot do evento
docker exec -it cashflow-mongo mongosh -u cashflow -p CHANGE_ME_IN_PROD \
  --authenticationDatabase admin --eval \
  "db.getSiblingDB('cashflow_consolidation').daily_balances.deleteMany({merchantId: '0193e7a8-d8f0-7c5e-9b21-3f9f8a4d1c00'})"

# Republicar os eventos a partir da tabela Ledger (script SQL custom — pendente).
```

---

## 4. Rotação de secrets

### 4.1 Postgres / Mongo / Rabbit (passwords)

1. Atualize o valor em `infra/.env` (substituir `CHANGE_ME_IN_PROD`).
2. Atualize secrets no orchestrador (em prod: AKV / GitHub Secrets / Vault).
3. Mude a senha no DB:
   ```bash
   docker exec -it cashflow-postgres \
     psql -U cashflow -d cashflow_ledger -c \
     "ALTER USER cashflow WITH PASSWORD 'NEW_SECRET';"
   ```
4. Rebuild + restart das apps que consomem (sem perder estado):
   ```bash
   docker compose -f infra/docker-compose.yml --profile app up -d --force-recreate
   ```

### 4.2 Keycloak client secret (`cashflow-api`)

1. Admin Console (`http://localhost:8080`) → Realm `cashflow` → Clients → `cashflow-api` → Credentials → **Regenerate Secret**.
2. Atualize `CLIENT_SECRET` no `.env` e em qualquer script (`scripts/make-perf.sh`, `scripts/chaos-validate.sh`).
3. Restart das apps que validam JWT (não estritamente necessário se a secret é só usada por scripts).

### 4.3 Keycloak admin password

```bash
# Stop Keycloak
docker compose -f infra/docker-compose.yml stop keycloak
# Mude KEYCLOAK_ADMIN_PASSWORD em infra/.env
# Restart
docker compose -f infra/docker-compose.yml start keycloak
```

### 4.4 Rotação de JWKS (em prod)

```bash
# Force regeneração da chave de assinatura ativa
curl -fsS -X POST \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  http://localhost:8080/admin/realms/cashflow/keys/rotate
```

> APIs cacheam JWKS por 5 min ([ADR-0011](adr/ADR-0011-keycloak-auth.md)). Tokens emitidos com a chave antiga continuam válidos até expirar (5 min default).

---

## 5. Resposta a SLO breach

### 5.1 Latência P95 Consolidado > 500 ms por 10 min

**Checklist (em ordem):**

1. **Cache hit rate** caiu? — Grafana → dashboard *RED* → painel `cashflow_cache_hit_ratio`. Se < 90 %, investigar invalidação anormal (deploy, mudança de chave).
2. **Mongo latência** subiu? — `mongosh --eval "db.serverStatus().opLatencies"`. Comparar com baseline.
3. **CPU/RAM do consolidation-api**? — `docker stats --no-stream`. Se saturado, escalar.
4. **Lock contention no stampede lock** (Redis SET NX)? — checar logs do `GetDailyBalanceHandler` para mensagens `lock lost`.
5. **Polly circuit breaker** aberto? — métrica `cashflow_polly_breaker_open` no Prometheus.

### 5.2 Lag de projeção P95 > 30 s por 5 min

Veja §1 (diagnóstico de lag). Ação imediata: escalar `consolidation-worker` (mais consumers) e investigar throughput do dispatcher do Outbox.

### 5.3 Outbox pending > 1000 por 2 min

```bash
# Verifica se o dispatcher está fazendo progresso
docker exec -it cashflow-postgres \
  psql -U cashflow -d cashflow_ledger -c \
  "SELECT COUNT(*) FILTER (WHERE \"DeliveredOn\" IS NULL) AS pending,
          COUNT(*) FILTER (WHERE \"DeliveredOn\" >= NOW() - INTERVAL '1 min') AS delivered_last_min
   FROM messaging.\"OutboxMessage\";"
```

Se `delivered_last_min = 0`: RabbitMQ down ou dispatcher travado. Reinicie `ledger-api`:

```bash
docker compose -f infra/docker-compose.yml restart ledger-api
```

### 5.4 Taxa de erro Consolidado > 5% por 5 min

1. Loki query no Grafana: `{app="cashflow.consolidation.api"} |= "ERR" | json | level="Error"`.
2. Verificar Polly breaker status; se aberto, descobrir downstream impactado (Mongo? Redis?).
3. Se for transitory, deixar Polly tratar; se persistente, page on-call.

### 5.5 Disponibilidade Ledger < 99.9 % (burn rate 14.4× em 1 h)

1. `docker compose ps ledger-api` — está saudável? Healthcheck verde?
2. `docker logs --tail=300 cashflow-ledger-api | grep -E "ERR|CRIT"`.
3. Postgres acessível? `docker exec -it cashflow-postgres pg_isready -U cashflow`.
4. Rabbit acessível (para Outbox publisher)? `rabbitmq-diagnostics -q ping`.
5. Página o on-call **mesmo** se for resolvido — investigar root cause.

---

## 6. Procedimentos de manutenção

### 6.1 Backup ad-hoc

```bash
# Postgres
docker exec -t cashflow-postgres pg_dumpall -U cashflow > backup-pg-$(date +%F).sql

# Mongo
docker exec -t cashflow-mongo mongodump \
  -u cashflow -p CHANGE_ME_IN_PROD --authenticationDatabase admin \
  --db cashflow_consolidation --archive > backup-mongo-$(date +%F).archive
```

### 6.2 Restore

```bash
# Postgres
cat backup-pg-2026-05-14.sql | docker exec -i cashflow-postgres psql -U cashflow

# Mongo
docker exec -i cashflow-mongo mongorestore \
  -u cashflow -p CHANGE_ME_IN_PROD --authenticationDatabase admin \
  --archive < backup-mongo-2026-05-14.archive
```

### 6.3 Limpeza de OutboxMessage antigas

MassTransit Outbox tem cleanup nativo (`MessageDeliveryLimit` + `DuplicateDetectionWindow`). Para rodar manualmente em prod:

```sql
DELETE FROM messaging."OutboxMessage"
WHERE "DeliveredOn" IS NOT NULL
  AND "DeliveredOn" < NOW() - INTERVAL '7 days';
```

### 6.4 Limpeza de `processed_events` no Mongo

TTL automático já remove em 7d (`expireAfterSeconds: 604800`). Para validar:

```bash
docker exec -it cashflow-mongo mongosh -u cashflow -p CHANGE_ME_IN_PROD \
  --authenticationDatabase admin --eval \
  "db.getSiblingDB('cashflow_consolidation').processed_events.getIndexes()"
```

Esperado: índice `{ processedAt: 1 }` com `expireAfterSeconds: 604800`.

---

## 7. Comandos úteis (cheat sheet)

```bash
# Subir / derrubar
make up                                  # core + app
make down                                # stop preservando volumes
make nuke                                # CUIDADO: apaga volumes
make logs SERVICE=ledger-api             # follow logs

# Testes
make test                                # dotnet test (unit + integration + arch)
make perf                                # k6 NFR 50 req/s × 60s
make chaos-validate                      # NFR-A-01 isolamento

# Acesso direto às APIs (bypass Gateway)
http://localhost:8001/swagger            # Ledger Swagger UI
http://localhost:8002/swagger            # Consolidation Swagger UI

# Acesso aos UIs operacionais (profile tools)
docker compose -f infra/docker-compose.yml --profile tools up -d
http://localhost:5050                    # pgAdmin
http://localhost:8081                    # Mongo Express
http://localhost:5540                    # RedisInsight
http://localhost:15672                   # RabbitMQ Management
http://localhost:3000                    # Grafana (admin/admin)
http://localhost:9090                    # Prometheus
```

---

## 8. Escalação / contatos

- **Slack:** `#cashflow-oncall` (placeholder — configurar antes do go-live).
- **PagerDuty:** schedule `cashflow-primary` (placeholder).
- **Repositório de incidentes:** issues GitHub label `incident`.
- **Owner do serviço:** @marcelo.

Para postmortem: usar template em `docs/incidents/_template.md` (a criar quando o primeiro incidente acontecer — não escrever especulativamente).
