# Architecture Decision Records — Cashflow

Decisões estruturais do projeto em formato **MADR** (Markdown Architectural Decision Records). Cada ADR descreve **contexto**, **direcionadores**, **alternativas consideradas**, **decisão escolhida** e **consequências** (positivas, negativas, riscos e plano de revisão).

| # | Título | Status | Tags |
|---|---|---|---|
| [ADR-0001](ADR-0001-microsservicos-event-driven.md) | Microsserviços event-driven em vez de monolito modular | Accepted | `arquitetura`, `style` |
| [ADR-0002](ADR-0002-dotnet-9-clean-arch.md) | .NET 9 + Clean Architecture como base de cada bounded context | Accepted | `runtime`, `clean-architecture` |
| [ADR-0003](ADR-0003-clean-architecture.md) | Clean Architecture + DDD-lite como organização interna de cada serviço | Accepted | `arquitetura`, `domain`, `ddd` |
| [ADR-0004](ADR-0004-cqrs-fisico.md) | CQRS físico (write Postgres / read Mongo) em vez de CQRS lógico | Accepted | `cqrs`, `dados` |
| [ADR-0005](ADR-0005-postgres-write-side.md) | PostgreSQL 16 como write store do Ledger | Accepted | `dados`, `postgres` |
| [ADR-0006](ADR-0006-mongo-read-side.md) | MongoDB 7 como read store da Consolidação | Accepted | `dados`, `mongodb` |
| [ADR-0007](ADR-0007-rabbitmq-vs-kafka.md) | RabbitMQ + MassTransit em vez de Apache Kafka | Accepted | `mensageria`, `rabbitmq` |
| [ADR-0008](ADR-0008-masstransit-outbox.md) | MassTransit `EntityFrameworkOutbox` em vez de Outbox manual | Accepted | `outbox`, `consistencia` |
| [ADR-0009](ADR-0009-redis-cache.md) | Redis 7 como cache aside com stampede lock simples (sem Redlock) | Accepted | `cache`, `redis` |
| [ADR-0010](ADR-0010-otel-observability.md) | OpenTelemetry + Grafana stack para observabilidade | Accepted | `otel`, `grafana` |
| [ADR-0011](ADR-0011-keycloak-auth.md) | Keycloak 25 (OIDC) como Identity Provider | Accepted | `seguranca`, `keycloak`, `oidc` |
| [ADR-0012](ADR-0012-yarp-gateway.md) | YARP como API Gateway | Accepted | `gateway`, `yarp` |
| [ADR-0013](ADR-0013-test-strategy.md) | Estratégia de testes — pirâmide unit + integration + architecture + k6 | Accepted | `testes`, `qualidade` |
| [ADR-0014](ADR-0014-tls-edge-termination.md) | TLS terminado no edge em prod; HTTP em dev | Accepted | `tls`, `https`, `infra` |

## Template

Novos ADRs devem usar o template em [`_template/ADR-0000-template.md`](_template/ADR-0000-template.md) (mesmo template aplicado em todos os ADRs acima).

## Quando criar um ADR

- Decisão **estrutural** que tem alternativas plausíveis (escolher Postgres vs MongoDB, RabbitMQ vs Kafka).
- Decisão que afeta **múltiplos componentes** (auth, observabilidade, padrão de mensageria).
- Decisão que será **difícil de reverter** sem retrabalho relevante.
- Decisão que tem **trade-offs explícitos** que outros engenheiros vão querer entender depois.

Não criar ADR para:
- Escolha de biblioteca utilitária (ex.: FluentValidation vs DataAnnotations).
- Pequenas decisões de implementação (nome de campo, formatação).
- Padrões já documentados em CLAUDE.md ou no blueprint.
