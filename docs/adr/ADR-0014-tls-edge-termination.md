# ADR-0014: TLS terminado no edge em prod; HTTP em dev

- **Status:** Accepted
- **Data:** 2026-05-13
- **Decisores:** @marcelo
- **Tags:** `seguranca`, `tls`, `https`, `infra`

## Contexto e problema

NFR-S-04 (SHOULD) pede HTTPS no Gateway. A questão é **onde** terminar TLS:

- **Dev local:** Gateway escuta HTTP `:8000`. Cert autoassinado introduz fricção em `make up` (`docker compose` precisa de cert válido para healthcheck; navegador exige confiança; HSTS sobre HTTP é inerte).
- **Prod:** TLS é não-negociável.

Precisamos decidir uma política coerente que não atrapalhe o dev nem deixe prod inseguro.

## Direcionadores da decisão

- **D1.** NFR-S-04 (SHOULD): HTTPS no gateway em prod.
- **D2.** Dev local não pode exigir cert setup manual — `make up` deve funcionar de primeira.
- **D3.** HSTS só faz sentido em HTTPS — header HSTS sobre HTTP é inerte por spec.
- **D4.** Não duplicar a cadeia: TLS no Gateway YARP + TLS edge é overhead sem ganho.

## Alternativas consideradas

### Opção A — TLS no Gateway YARP + cert autoassinado em dev
- Gateway escuta `:443` em dev e prod.
- **Contras:** dev precisa instalar cert na trust store (instrução extra); browsers gritam; perda de tempo sem ganho de segurança em loopback.

### Opção B — TLS terminado em edge externo em prod; HTTP no Gateway YARP em prod e dev — **escolhida**
- Em prod: Azure Application Gateway / Cloudflare / Nginx sidecar termina TLS e proxia HTTP para o YARP.
- Em dev: HTTP `:8000` direto.
- HSTS é habilitado no edge externo em prod (`max-age=31536000; includeSubDomains; preload`).
- **Prós:** dev trivial; prod usa cert válido gerenciado por L4; descomplica rotação.

### Opção C — TLS no Gateway YARP em prod, HTTP em dev
- Gateway termina TLS em prod com cert do Let's Encrypt via reverse-proxy do compose.
- **Contras:** YARP + cert manager em prod é mais código; rotação manual; menos elegante que delegar a CDN/L7.

## Decisão

Escolhemos a **Opção B — TLS terminado em edge externo em prod / HTTP em dev**.

**Topologia em prod:**

```
Cliente
  │  HTTPS
  ▼
[Edge: App Gateway / Cloudflare / Nginx sidecar]
  │  HTTP (rede privada VNet/VPC)
  ▼
Gateway YARP (HTTP :8080)
  │  HTTP (cashflow-net interna)
  ▼
Ledger.Api / Consolidation.Api
```

**Configurações:**

- **Edge:**
  - Cert válido (Let's Encrypt / AWS ACM / Azure Key Vault).
  - HSTS `max-age=31536000; includeSubDomains; preload`.
  - TLS 1.2+ (PCI-DSS), ciphers modernos.
  - HTTP/2 (ou HTTP/3) entre cliente e edge.
- **Gateway YARP:**
  - Lê `X-Forwarded-Proto` / `X-Forwarded-For` (cliente real para rate-limit por IP).
  - Sem HSTS no YARP (já tratado no edge).
  - Healthcheck HTTP `:8080/health` para o L7 monitorar.
- **Dev:**
  - HTTP `:8000` direto, sem cert, sem HSTS.
  - Documentado no README §6.

**Atenção dev → prod:** quando promover para prod, **não** habilite HSTS no YARP além do edge. Headers HSTS duplos não causam falha mas sinalizam confusão arquitetural.

## Consequências

### Positivas
- **`make up` funciona de primeira** sem configurar cert.
- **Cert em prod** gerenciado por L4/L7 com auto-renew.
- **HSTS efetivo** no edge (não inerte).
- **HTTP/2 / HTTP/3** ganho de performance gerenciado pelo edge.

### Negativas / Trade-offs aceitos
- **Sem TLS interno** entre edge e YARP em prod — a rede privada (VNet/VPC) é o controle. Para zero-trust, considerar mTLS em evolução (service mesh).
- **Sem HTTPS local** — testes que dependem de HSTS/cookies `Secure` exigem ambiente staging.
- **Dependência externa** — edge deve estar disponível; failover entre cloud regions é planejamento separado.

### Riscos e mitigações
| Risco | Probabilidade | Impacto | Mitigação |
|---|---|---|---|
| `X-Forwarded-Proto` não propagado quebra detecção HTTPS no app | média | baixo | YARP/ASP.NET configura `ForwardedHeadersOptions`; integration test cobre |
| Cert expirar | baixa | alto | Auto-renew via cert-manager / ACM; alerta SLI |
| Edge mal configurado expõe HTTP | baixa | alto | Pipeline IaC com policy `requireHttps: true` |

## Plano de revisão

- Reavaliar **mTLS interno** (Linkerd / Istio) se compliance exigir zero-trust.
- Métrica de saúde: cert expira em > 14 dias; redirect HTTP→HTTPS rate < 1% (clientes legados).

## Referências

- [Mozilla SSL Configuration Generator](https://ssl-config.mozilla.org/).
- [HSTS Preload](https://hstspreload.org/).
- [Microsoft — ASP.NET Core HSTS](https://learn.microsoft.com/aspnet/core/security/enforcing-ssl).
- ADRs relacionadas: [ADR-0012](ADR-0012-yarp-gateway.md), [ADR-0011](ADR-0011-keycloak-auth.md).
