# ADR-0011: Keycloak 25 (OIDC) como Identity Provider

- **Status:** Accepted
- **Data:** 2026-05-13
- **Decisores:** @marcelo
- **Tags:** `seguranca`, `auth`, `keycloak`, `oidc`

## Contexto e problema

NFR-S-01..05 (MUST/SHOULD) exigem:

- Autenticação **OIDC/JWT**, com validação no Gateway.
- Autorização role-based (`merchant`, `admin`) + resource-based (`merchantId == claim`).
- Refresh tokens / PKCE / Authorization Code suportados (para o frontend MAY).
- Realm provisionado de forma reproduzível (sem clicar em GUI).
- Em dev, sem clicar em interface — `make up` traz o IdP pronto.

## Direcionadores da decisão

- **D1.** NFR-S-01 (MUST): OIDC/JWT.
- **D2.** NFR-S-02 (MUST): role + resource-based auth — exige claim custom `merchantId`.
- **D3.** Suporte `Direct Access Grant` para CI/k6/chaos scripts (sem browser flow).
- **D4.** Provisionamento via JSON importável (sem `kcadm.sh` manual).
- **D5.** Sem custo de licença em dev.

## Alternativas consideradas

### Opção A — Keycloak 25 — **escolhida**
- IdP OSS Red Hat.
- **Prós:** OIDC + SAML; protocol mappers para claims custom; realm importável JSON; Admin Console rico; comunidade ativa.
- **Contras:** consumo de memória (~512 MB); startup lento (60s+ no primeiro start).

### Opção B — Auth0 / Okta / Azure AD B2C
- SaaS.
- **Contras:** **falha em local-dev sem internet**; custo em prod; lock-in.

### Opção C — IdentityServer 4 (custom .NET)
- Implementação própria em .NET.
- **Contras:** desde a v4 OSS, IS5+ é proprietário (Duende); manter realm/scopes/claims em código vs. JSON é mais frágil.

### Opção D — ORY Hydra + Kratos
- OSS, design moderno.
- **Contras:** ferramental menor que Keycloak; provisioning menos polido para o desafio.

## Decisão

Escolhemos **Keycloak 25.0** (`quay.io/keycloak/keycloak:25.0`) com:

- **Realm** `cashflow` importado de `infra/keycloak/realm-cashflow.json` (mountado em `/opt/keycloak/data/import/`).
- **Client confidential** `cashflow-api` com `directAccessGrantsEnabled: true` (password grant para CI/scripts) e `serviceAccountsEnabled: true` (client credentials para chamadas internas).
- **Roles** `merchant` (`merchant1@cashflow.local`) e `admin` (`admin@cashflow.local`).
- **Client scope** `merchantId-scope` com **protocol mapper** `oidc-usermodel-attribute-mapper` que injeta o atributo `merchantId` do user no access token.
- **Postgres como backend** do Keycloak (mesmo Postgres do Ledger, schema próprio gerenciado pelo Keycloak).
- **`accessTokenLifespan: 300`** (5 min) — scripts de chaos/perf regeneram token a cada round.

**Issuer alignment crítico** (memória `webapp_factory_integration.md` do projeto):
- `KC_HOSTNAME=http://localhost:8080` para que o `iss` do JWT bata com o `Authority` configurado nas APIs.
- `KC_HOSTNAME_BACKCHANNEL_DYNAMIC=true` para que o discovery doc reflita o `Host` da request — sem isso, a API dentro do container resolve `iss=http://localhost:8080` mas faria fetch de JWKS em loopback (404).
- Cada serviço .NET no compose tem `extra_hosts: ["localhost:host-gateway"]` para que `localhost` resolva ao host (Docker Desktop publica `:8080`).
- `Keycloak__MetadataAddress=http://keycloak:8080/realms/cashflow/.well-known/openid-configuration` aponta para a DNS interna — `Authority` continua `http://localhost:8080/realms/cashflow` (validação do `iss`).

**Authorization policies** nas APIs:

```csharp
options.AddPolicy(AuthorizationPolicies.RequireMerchant, p =>
    p.RequireAuthenticatedUser().RequireRole("merchant"));

options.AddPolicy(AuthorizationPolicies.RequireAdmin, p =>
    p.RequireAuthenticatedUser().RequireRole("admin"));
```

Resource-based check no `BalancesEndpoints`: `httpContext.CanAccessMerchant(merchantId)` compara o claim `merchantId` do JWT com o route segment.

## Consequências

### Positivas
- Realm reproduzível — `make up` em qualquer máquina traz IdP idêntico.
- Password grant em CI/scripts sem hack adicional.
- Claim `merchantId` propaga até o YARP (transformação) e até as APIs.
- Refresh tokens disponíveis para o frontend (NextAuth Keycloak provider).

### Negativas / Trade-offs aceitos
- **Startup ~60s** no primeiro `make up` (Keycloak import + migrations).
- **Memória ~512 MB** para Keycloak em dev.
- **JWKS cache** 5 min — rotação de chave em prod exige plano (`POST /admin/realms/{realm}/clear-keys-cache`).

### Riscos e mitigações
| Risco | Probabilidade | Impacto | Mitigação |
|---|---|---|---|
| Realm JSON drift entre dev e prod | média | alto | Único source of truth `infra/keycloak/realm-cashflow.json`; CI valida import |
| JWKS endpoint indisponível bloqueia auth | baixa | alto | Polly `keycloak-jwks` pipeline (Timeout 5s + Retry 2x); JWKS cacheado 5 min |
| Service-account sem `merchantId` quebra `RequireMerchant` | média | alto | Documentado em `scripts/make-perf.sh` (default = password grant) |
| Secret `cashflow-api-secret` exposto | alta | médio | `infra/.env` no `.gitignore`; em prod, vault |

## Plano de revisão

- Reavaliar quando precisar de multi-realm (multi-tenant).
- Métrica de saúde: latência de `/protocol/openid-connect/token` < 200 ms; JWKS fetch < 100 ms.

## Referências

- [Keycloak 25 docs](https://www.keycloak.org/documentation).
- [OIDC Core spec](https://openid.net/specs/openid-connect-core-1_0.html).
- [.NET JwtBearer middleware](https://learn.microsoft.com/aspnet/core/security/authentication/jwt-authn).
- ADRs relacionadas: [ADR-0012](ADR-0012-yarp-gateway.md), [ADR-0014](ADR-0014-tls-edge-termination.md).
