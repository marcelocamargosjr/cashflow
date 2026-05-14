# Sequence diagrams — Cashflow

Diagramas de sequência dos fluxos críticos. Mermaid `sequenceDiagram` (compatível com preview do GitHub).

| Arquivo | Fluxo | Cobre |
|---|---|---|
| [register-entry.mmd](register-entry.mmd) | `POST /ledger/api/v1/entries` | JWT + rate-limit + idempotency + Outbox transacional + consumer idempotente |
| [query-balance.mmd](query-balance.mmd) | `GET /consolidation/api/v1/balances/{merchantId}/daily` | Cache aside + stampede lock (won / lost / fallback) + Polly pipeline `mongo-read` |
| [chaos-isolation.mmd](chaos-isolation.mmd) | `make chaos-validate` | Comprova NFR-A-01: Ledger sobrevive a Consolidation offline; backlog Outbox drena em catch-up < 60s |

## Visualização

GitHub renderiza Mermaid sequence diagrams nativamente. Localmente: VS Code com *Markdown Preview Mermaid Support* ou https://mermaid.live.
