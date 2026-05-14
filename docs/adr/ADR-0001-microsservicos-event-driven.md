# ADR-0001: Microsserviços event-driven em vez de monolito modular

> Exemplo preenchido para servir de modelo aos demais ADRs.

- **Status:** Accepted
- **Data:** 2026-05-13
- **Decisores:** @marcelo
- **Tags:** `arquitetura`, `style`

## Contexto e problema

O desafio exige um **Sistema de Fluxo de Caixa** com dois serviços de negócio (Lançamentos e Consolidado Diário). O requisito não-funcional crítico estabelece:

> "O serviço de controle de lançamento **não deve ficar indisponível** se o sistema de consolidado diário cair. Em dias de picos, o serviço de consolidado diário recebe **50 requisições por segundo, com no máximo 5% de perda**."

Precisamos decidir o estilo arquitetural que melhor atende a esses requisitos dentro de um prazo de ~2 semanas part-time.

## Direcionadores da decisão

- **D1.** NFR-A-01 (MUST): Ledger sobrevive a Consolidation indisponível.
- **D2.** NFR-P-01 (MUST): 50 req/s sustentado em Consolidation com erro < 5%.
- **D3.** Demonstrar conhecimento empírico de padrões (DDD, CQRS, Event-Driven).
- **D4.** Operação simples (rodar tudo via `docker compose`).
- **D5.** Tempo limitado (~2 semanas).

## Alternativas consideradas

### Opção A — Monolito (.NET único)
- Único projeto com pastas separando "Ledger" e "Consolidation".
- **Prós:** velocidade inicial, deploy único, transações ACID em todo o sistema.
- **Contras:** **não atende D1** — se o monolito cai, Ledger cai junto; isolamento de falha exigiria mecanismos elaborados dentro do mesmo processo.

### Opção B — Monolito modular com Module Federation
- Mesmo processo, mas módulos com interface bem definida e DBs separados.
- **Prós:** isolamento lógico, simplicidade operacional.
- **Contras:** falha de runtime (memória, deadlock, OOM) ainda afeta os dois módulos. **Não atende D1 de verdade**.

### Opção C — Microsserviços event-driven (escolhida)
- 2 microsserviços (Ledger e Consolidation) + broker assíncrono (RabbitMQ) + Outbox + CQRS físico.
- **Prós:** isolamento real de falha (D1), escalabilidade independente (D2), demonstra empiricamente os padrões pedidos (D3).
- **Contras:** mais complexidade operacional (D4 atenuado por `docker compose`), curva de aprendizado para Outbox/MassTransit (D5 atenuado por usar bibliotecas testadas).

### Opção D — Serverless (Azure Functions)
- Functions para cada operação + Service Bus + Cosmos.
- **Prós:** escala automática.
- **Contras:** infraestrutura cloud-bound (D4 viola "tudo via docker compose"); custo de aprendizado e mock local.

## Decisão

Escolhemos a **Opção C — Microsserviços event-driven**.

Razões principais:
- É a **única** opção que atende **D1** sem mecanismos artificiais — quando Consolidation cai, Ledger continua aceitando lançamentos e enfileirando eventos no Outbox; quando Consolidation volta, processa o backlog.
- Atende **D2** com cache + rate-limit + horizontalização independente.
- Demonstra **D3** explicitamente: Outbox, CQRS, idempotência, eventual consistency.
- **D4** é mitigado por um único `docker-compose.yml` que orquestra tudo.
- **D5** é mitigado por usar **MassTransit Outbox nativo** (zero código de plumbing).

## Consequências

### Positivas
- Isolamento de falha real (não simulado).
- Escalabilidade horizontal independente por serviço.
- Demonstra os padrões esperados pelo desafio.
- Reuso possível em outros contextos do portfólio.

### Negativas / Trade-offs aceitos
- **Eventual consistency** — UI mostra "atualizado há Xs"; mitigado documentando explicitamente.
- **Operação mais complexa** — 5+ containers no compose; mitigado por `make up`/`make down`.
- **Debug distribuído** — exige traces distribuídos; mitigado com OTel + Tempo.

### Riscos e mitigações
| Risco | Probabilidade | Impacto | Mitigação |
|---|---|---|---|
| Drift de evento entre serviços | média | alto | Versionamento explícito de evento + JSON Schema versionado |
| Outbox crescer indefinidamente em falha do broker | baixa | médio | Métrica `cashflow.outbox.pending` + alerta + DLQ |
| Duplicação de processamento | média | médio | Idempotência via `processed_events` (TTL 7d) |

## Plano de revisão

- Reavaliar se volume crescer para > 10x o estimado (10k lançamentos/merchant/mês × 1k merchants).
- Métricas de saúde: lag P95 < 5s, outbox pending < 100, erro de consume < 1%.

## Referências

- ADR-0007 — Outbox Pattern (MassTransit nativo).
- [Microservices.io — Microservice Architecture pattern](https://microservices.io/patterns/microservices.html)
- [Pat Helland — Data on the Outside vs Data on the Inside](https://queue.acm.org/detail.cfm?id=3415014)
