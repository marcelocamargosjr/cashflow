# LGPD — Lei Geral de Proteção de Dados (Lei nº 13.709/2018)

> Mapeamento de dados pessoais coletados, base legal, princípios aplicados, direitos do titular e medidas técnicas e organizacionais adotadas no Cashflow.

---

## 1. Resumo executivo

O Cashflow processa dados pessoais de **comerciantes** (não de consumidores finais). Os dados são tratados com base no Art. 7º, V da LGPD — **execução de contrato** — e armazenados pelo prazo mínimo exigido pela legislação fiscal brasileira (5 anos a partir do encerramento do exercício, Art. 195 §1º CTN c/c Decreto 9.580/2018).

Princípios aplicados (LGPD Art. 6º): **finalidade**, **adequação**, **necessidade**, **livre acesso**, **qualidade**, **transparência**, **segurança**, **prevenção**, **não discriminação** e **responsabilização**.

---

## 2. Dados pessoais coletados

| Dado | Onde está armazenado | Categoria LGPD | Base legal | Finalidade | Retenção |
|---|---|---|---|---|---|
| **Email do merchant** | Keycloak (`realm cashflow` → `users`) | Identificação eletrônica | Art. 7º, V (execução de contrato) | Autenticação | Até cancelamento + 5 anos |
| **Nome / sobrenome** | Keycloak | Dado pessoal comum | Art. 7º, V | Identificação no console | Até cancelamento + 5 anos |
| **Senha** (hash) | Keycloak (PBKDF2/Argon2 conforme config interna do Keycloak) | Dado sensível por exposição (não dado sensível LGPD) | Art. 7º, V | Autenticação | Até reset; rotacionada conforme política |
| **`merchantId`** (UUID pseudonimizado) | Postgres `entries.merchant_id`, Postgres `idempotency_keys`, Mongo `daily_balances.merchantId`, Redis cache keys, logs estruturados, métricas Prometheus | Pseudônimo (LGPD Art. 13 §IV) | Art. 7º, V + Art. 11 (anonimização parcial) | Vincular lançamentos ao titular sem expor identidade | Até cancelamento + 5 anos (obrigação fiscal) |
| **Valor de lançamento** (`amount.value`) | Postgres `entries.amount`, Mongo `daily_balances.totalCredits/totalDebits` | Dado pessoal financeiro | Art. 7º, V | Registro contábil | 5 anos (Art. 195 CTN) |
| **Descrição** do lançamento (`description`) | Postgres `entries.description` | Dado pessoal narrativo | Art. 7º, V | Auditoria interna do merchant | 5 anos |
| **Categoria** (`category`) | Postgres `entries.category`, Mongo `daily_balances.byCategory[]` | Dado pessoal financeiro | Art. 7º, V | Segmentação de relatórios | 5 anos |
| **Data de competência** (`entryDate`) | Postgres `entries.entry_date`, Mongo `daily_balances.date` | Dado pessoal financeiro | Art. 7º, V | Localizar projeção | 5 anos |
| **`correlationId` / `traceId`** | Logs (Loki), traces (Tempo) | Metadado técnico (não-pessoal isolado, mas pode ser correlacionado) | Art. 7º, IX (legítimo interesse — observabilidade) | Diagnóstico operacional | 14 dias (retenção dev); 90 dias em prod |

> **Não coletamos:** CPF, CNPJ, endereço físico, telefone, IP do consumidor final, geolocalização, dados de saúde, biométricos, raça, religião ou opinião política.

---

## 3. Princípios aplicados

### 3.1 Minimização (Art. 6º, III)

- **Logs nunca contêm:** `amount.value` numérico, `description` em texto livre, CPF/CNPJ (que aliás não coletamos).
- **Logs contêm apenas:** `merchantId` (UUID pseudonimizado), `entryId` (UUID), `correlationId`, `traceId`, `spanId`, nível de log, mensagem estruturada com placeholders.
- **Verificação:** `grep -r "cpf\|cnpj\|amount.value\|description" --include="*.cs" src/` deve retornar **zero** ocorrências em chamadas de log (validado por F7.1 §1.5 hardening).

Exemplo de log compliant:

```csharp
logger.LogInformation("Entry {EntryId} registered for merchant {MerchantId}", entry.Id, entry.MerchantId);
// NÃO: logger.LogInformation($"Entry {entry.Id} for {entry.MerchantId} amount {entry.Amount.Value} desc {entry.Description}");
```

### 3.2 Pseudonimização (Art. 13 §IV)

- Identificador do titular é **UUID** (`merchantId`), nunca chave de negócio (CNPJ, email).
- Reidentificação só é possível via Keycloak (que mantém o link `merchantId ↔ email`), em ambiente segregado com controle de acesso role-based.

### 3.3 Criptografia em repouso (Art. 46)

- **Dev:** volumes Docker criptografados via dm-crypt no host (responsabilidade do operador local).
- **Prod (documentado, não implementado neste MVP):**
  - Postgres → TDE via Azure DB for PostgreSQL ou Postgres `pg_crypto` com customer-managed keys.
  - Mongo → encryption-at-rest via WiredTiger + KMIP (Atlas / self-hosted com keyfile rotacionada).
  - Redis → `requirepass` + TLS in-transit; persistence em volume criptografado.
  - Backups → criptografados antes de upload (KMS-wrapped DEK).

### 3.4 Criptografia em trânsito (Art. 46)

- **Dev:** HTTP no gateway (HSTS sobre HTTP é inerte — ver [ADR-0014](adr/ADR-0014-tls-edge-termination.md)).
- **Prod:** TLS 1.2+ terminado no edge (Azure Application Gateway / Cloudflare / Nginx sidecar). HSTS `max-age=31536000; includeSubDomains; preload`. Ciphers modernos (Mozilla "Intermediate").

### 3.5 Segregação por finalidade (Art. 6º, II — adequação)

- **Postgres** armazena dados transacionais (lançamentos).
- **Mongo** armazena dados agregados de leitura (saldos consolidados) — **não** armazena dados pessoais granulares.
- **Redis** armazena cópia transitória (TTL 60s) do agregado.
- **Loki/Tempo/Prometheus** armazenam apenas metadados técnicos.

---

## 4. Direitos do titular (LGPD Art. 18)

| Direito | Implementação atual | Plano |
|---|---|---|
| **Confirmação da existência de tratamento** (Art. 18, I) | `GET /api/v1/entries?merchantId=...` (autenticado) | MUST — entregue |
| **Acesso aos dados** (Art. 18, II) | `GET /api/v1/entries` + `GET /balances/*` paginados | MUST — entregue |
| **Correção** (Art. 18, III) | `POST /api/v1/entries/{id}/reverse` (estorno) + via Keycloak Account Console (`/realms/cashflow/account`) para dados de perfil | MUST — entregue |
| **Anonimização, bloqueio ou eliminação** (Art. 18, IV) | **Não implementado** | Evolução: `POST /admin/anonymize-merchant/{id}` que reescreve `description` para hash, mantém `merchantId` (pseudônimo) e totais (necessários para auditoria fiscal). Eliminação real só após o prazo legal de 5 anos. |
| **Portabilidade** (Art. 18, V) | **Não implementado** | Evolução: `GET /admin/export-merchant/{id}` retornando JSON com todos os lançamentos + projeções agregadas. |
| **Eliminação dos dados** (Art. 18, VI) | **Conflita com obrigação fiscal** (5 anos) | Documentar limite legal. Após 5 anos, processo batch limpa entries. |
| **Informação sobre compartilhamento** (Art. 18, VII) | Esta página + política de privacidade (a publicar) | SHOULD |
| **Revogação do consentimento** (Art. 18, IX) | Não aplicável — base legal é **execução de contrato**, não consentimento | — |

> **Prazo de resposta** (Art. 19): 15 dias corridos para requerimentos do titular.

---

## 5. Medidas técnicas e organizacionais

### 5.1 Técnicas

- **Autenticação** OIDC/JWT via Keycloak ([ADR-0011](adr/ADR-0011-keycloak-auth.md)).
- **Autorização** role-based + resource-based (`merchantId == claim`).
- **Rate limiting** por merchant (defesa contra enumeration / abuse).
- **Defense-in-depth:** JWT validado no Gateway e nas APIs.
- **Logs estruturados** sem PII granular (verificado por F7.1 hardening).
- **Backups criptografados** (planejado para prod).
- **TLS no edge** em prod.
- **Vulnerability scanning:** Trivy + Dependabot + CodeQL no pipeline (F9).

### 5.2 Organizacionais

- **Princípio do menor privilégio** em IAM (admin vs merchant).
- **Auditoria mínima** — `entries.created_at` / `entries.updated_at` rastreáveis (planejada tabela `audit_log` em evolução).
- **Onboarding** dos engenheiros inclui treinamento LGPD (placeholder).
- **DPO/Encarregado** a designar antes do release `v1.0.0` (placeholder).

---

## 6. Política de retenção

| Tipo de dado | Onde | Retenção | Justificativa |
|---|---|---|---|
| Lançamentos (`entries`) | Postgres | 5 anos a partir do encerramento do exercício fiscal | Art. 195 CTN |
| Projeções (`daily_balances`) | Mongo | Idem 5 anos (re-projetáveis dos eventos) | Idem |
| `processed_events` | Mongo (TTL automático) | **7 dias** | Idempotência: além disso, reentrega é evento "novo" para fins práticos |
| `OutboxMessage` | Postgres | Limpeza em 7 dias após `DeliveredOn != null` | Auditoria de publish |
| Logs (Loki) | Filesystem | 14d dev / 90d prod | Diagnóstico operacional |
| Traces (Tempo) | Filesystem | 7d dev / 30d prod | Diagnóstico |
| Métricas (Prometheus) | Filesystem | 24h dev / 30d prod | Dashboards SLO |
| Dados de Keycloak (users) | Postgres do Keycloak | Até cancelamento + 5 anos | Vínculo contratual |

---

## 7. Incidentes (Art. 48)

Em caso de incidente de segurança que possa causar risco ou dano relevante:

1. Conter o incidente (isolar serviço afetado, rotacionar secrets — ver [`runbook.md §4`](runbook.md#4-rotação-de-secrets)).
2. Investigar escopo (quais merchants foram afetados, quais dados).
3. Notificar a ANPD em até **2 dias úteis** (template em construção).
4. Notificar os titulares afetados em prazo razoável.
5. Postmortem com correção da raiz.

---

## 8. Transferência internacional (Art. 33)

- **Dev:** dados em ambiente local (laptop do operador).
- **Prod (hipotético):** se hosting for em região fora do Brasil (ex.: Azure East US), aplicar Art. 33 §I (cláusulas contratuais padrão da ANPD) ou §II (certificação). Preferência por região **Brazil South** para evitar transferência.

---

## 9. Verificações automatizadas no código

Lint regra (planejada como Roslyn analyzer custom):

```bash
# Não deve haver chamadas de log com dados sensíveis interpolados
grep -rEn 'logger\.Log\w+\(\$".*\{.*\.(Amount|Value|Description|Cpf|Cnpj|Email)' src/ \
  --include="*.cs" || echo "OK — sem leak de PII em log interpolation"
```

Em PRs:
- Revisão manual valida que novas features respeitam minimização.
- Architecture tests confirmam que `Cashflow.*.Domain` não importa `Microsoft.Extensions.Logging` (Domain não loga).
- Integration test específico verifica que `merchant secret` + JWT sem `merchantId` claim retornam 401/403.

---

## 10. Versão e revisão

- **Versão deste documento:** 1.0 (2026-05-14).
- **Próxima revisão programada:** ao publicar `v1.0.0` ou em até 12 meses, o que vier primeiro.
- **Aprovação:** @marcelo (placeholder de DPO).

> Este documento é um artefato técnico de demonstração e **não substitui** assessoria jurídica. Antes de publicar uma versão de produção do Cashflow, valide com advogado especialista em LGPD.
