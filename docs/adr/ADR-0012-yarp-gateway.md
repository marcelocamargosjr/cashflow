# ADR-0012: YARP como API Gateway (em vez de Nginx, Kong ou Envoy)

- **Status:** Accepted
- **Data:** 2026-05-13
- **Decisores:** @marcelo
- **Tags:** `gateway`, `yarp`, `rate-limit`, `auth`

## Contexto e problema

Precisamos de uma entrada única que:

- **Termine JWT** validando contra Keycloak (mesma configuração das APIs).
- Aplique **rate limit por merchant** com headroom 2× sobre o NFR (50 req/s → 120 req/s/merchant).
- Transforme paths (`/ledger/*` → `/api/v1/*` no backend) e remova headers sensíveis (`Cookie`).
- Propague `X-Correlation-Id` / `traceparent`.
- Termine TLS no edge em prod (HTTP em dev, [ADR-0014](ADR-0014-tls-edge-termination.md)).
- Tenha **healthcheck** consumível pelo Docker compose.

## Direcionadores da decisão

- **D1.** NFR-P-01 (MUST): Consolidation 50 req/s sustentado — rate-limit do gateway não pode ser auto-sabotagem.
- **D2.** NFR-S-01 (MUST): validação JWT no gateway.
- **D3.** Same-stack que as APIs (.NET 9) — simplifica build, debug e deploy.
- **D4.** Open-source com suporte sustentado.

## Alternativas consideradas

### Opção A — YARP 2.x — **escolhida**
- Reverse proxy oficial da Microsoft para .NET.
- **Prós:** mesma stack das APIs; configuração declarativa em `appsettings.json` (Routes/Clusters/Transforms); usa nativamente `Microsoft.AspNetCore.RateLimiting`; JWT Bearer reusa código; observabilidade OTel; healthchecks via `Microsoft.Extensions.Diagnostics.HealthChecks`.
- **Contras:** menos features prontas que Kong/Envoy (sem plugins JWT custom, sem hot-reload nativo de Routes — exige `OnChange`).

### Opção B — Nginx + njs
- Reverse proxy tradicional.
- **Contras:** rate-limit por claim JWT exige Lua/njs custom; validação JWT exige módulo extra; integração OTel é manual.

### Opção C — Kong Gateway
- Plataforma completa de API management.
- **Prós:** plugins JWT/rate-limit/CORS prontos.
- **Contras:** stack adicional (Postgres do Kong); RAM extra; over-engineering para 2 BCs.

### Opção D — Envoy
- Service mesh proxy.
- **Contras:** configuração mais complexa (xDS); melhor em service-mesh; over-engineering.

### Opção E — Ocelot
- Outro gateway .NET.
- **Contras:** menos investido pela Microsoft que YARP; YARP é a recomendação oficial atual.

## Decisão

Escolhemos **YARP 2.x** com configuração via `appsettings.json` (`ReverseProxy:Routes` + `Clusters`).

**Roteamento:**

| Rota | Match | Cluster | Auth | Rate limit | Transforms |
|---|---|---|---|---|---|
| `ledger-write` | `/ledger/{**catch-all}` | `ledger-api:8080` | `RequireMerchant` | `entry-write-policy` | `PathRemovePrefix=/ledger`, remove `Cookie`, set `X-Forwarded-Prefix=/ledger` |
| `consolidation-read` | `/consolidation/{**catch-all}` | `consolidation-api:8080` | `RequireMerchant` | `balance-read-policy` | mesma, com prefixo correspondente |

**Rate limit** (`Microsoft.AspNetCore.RateLimiting` sliding window):

- **`balance-read-policy`**: `120 req/s/merchant` (headroom 2× sobre o NFR de 50 req/s — proteção contra abuso, não teto).
- **`entry-write-policy`**: `30 req/s/merchant` (write é menos frequente).
- **Partition key:** claim `merchantId` do JWT; fallback IP para anônimos (que serão rejeitados pela auth de qualquer forma).

**`Cashflow.SharedKernel.AddCashflowResilience`** registra o pipeline `keycloak-jwks` (Timeout 5s + Retry 2x) usado pelo JWT bearer handler ao fetchar JWKS.

**Defense-in-depth:** APIs também validam JWT (caso o gateway seja burlado). Não é "duplicação" — é princípio canônico de segurança em camadas: o gateway é a defesa de borda; cada API valida novamente assumindo que a borda pode ter sido contornada (lateral movement, bypass de network policy, atacante já dentro da VNet/VPC).

## Consequências

### Positivas
- **Same-stack** — debug com breakpoints no YARP usando o mesmo VS/Rider.
- **Configuração declarativa** — toda a topologia em `appsettings.json`, sem código.
- **Rate-limit por claim** trivial via partition key.
- **Healthchecks** consumíveis pelo compose (`http://localhost:8080/health`).

### Negativas / Trade-offs aceitos
- **Sem hot-reload nativo** de Routes — `OnChange` + restart funciona, mas não é zero-downtime.
- **Sem plugin ecosystem** — features custom (signed URLs, OAuth introspection) ficam para nós.
- **YARP roda em-process** com ASP.NET — não é um L4 proxy puro.

### Riscos e mitigações
| Risco | Probabilidade | Impacto | Mitigação |
|---|---|---|---|
| Rate-limit auto-sabotagem (50 req/s NFR vs. limit) | média | alto | Headroom 2× explícito (120 req/s/merchant); k6 prova passagem |
| Path transform inconsistente entre rotas | média | médio | `PathRemovePrefix` igual para ambas; teste de integração no F6 cobre |
| YARP atualiza e quebra config | baixa | médio | CPM fixa versão; integration tests cobrem rota happy |

## Plano de revisão

- Reavaliar se features cross-cutting (OAuth introspection, mTLS upstream) virarem requisito.
- Métrica de saúde: latência overhead do gateway < 5ms p(95); taxa 429 < 1% em estado estacionário.

## Referências

- [YARP docs](https://microsoft.github.io/reverse-proxy/).
- [Microsoft.AspNetCore.RateLimiting](https://learn.microsoft.com/aspnet/core/performance/rate-limit).
- ADRs relacionadas: [ADR-0011](ADR-0011-keycloak-auth.md), [ADR-0014](ADR-0014-tls-edge-termination.md).
