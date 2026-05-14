# ADR-0003: Clean Architecture + DDD-lite como organização interna de cada serviço

- **Status:** Accepted
- **Data:** 2026-05-13
- **Decisores:** @marcelo
- **Tags:** `arquitetura`, `domain`, `clean-architecture`, `ddd`

## Contexto e problema

Dentro de cada bounded context (Ledger, Consolidation), precisamos decidir como organizar o código fonte. Os critérios são:
- isolar a lógica de negócio de qualquer dependência de infraestrutura para testá-la em unit tests rápidos;
- impedir, por estrutura, que um handler "vaze" detalhes de EF/Mongo no fluxo de aplicação;
- manter espaço para invariantes do agregado (Money, Entry, EntryStatus) sem cair em **Anemic Domain** ([ADR-0007](ADR-0007-rabbitmq-vs-kafka.md) define o restante).

## Direcionadores da decisão

- **D1.** NFR-M-01 (MUST): Domain sem dependência de Infra.
- **D2.** NFR-M-02 (MUST): violações detectadas em CI por architecture tests.
- **D3.** Domain testável em milissegundos, sem container.
- **D4.** Onboarding rápido — desenvolvedor familiar com .NET deve achar a estrutura óbvia.
- **D5.** Sem over-engineering: DDD "lite" (agregados + value objects + domain events), sem CQRS interno duplicado, sem repositórios "genericamente parametrizáveis".

## Alternativas consideradas

### Opção A — Onion / Hexagonal puro (4+ camadas com ports & adapters)
- `Domain` → `Application` → `Adapters/In` → `Adapters/Out` (mais cerimônia, mais arquivos).
- **Prós:** explicitamente neutra a frameworks.
- **Contras:** mais cerimônia para pouco ganho neste tamanho; "ports" vira interfaces que só têm 1 implementação (premature abstraction).

### Opção B — Clean Architecture com 4 projetos (Domain, Application, Infrastructure, Api) — **escolhida**
- Layering canônico Robert C. Martin / Ardalis Smith.
- **Prós:** convenção amplamente conhecida; layering enforceável por architecture tests; espaço claro para invariantes.
- **Contras:** ainda assim, 4 projetos por BC — mas o custo é compensado pelo isolamento.

### Opção C — Vertical Slice Architecture (features-first, sem camadas)
- 1 pasta por feature com command + handler + endpoint + persistência.
- **Prós:** descoberta rápida da feature; pouco code-spread quando você sabe o que procura.
- **Contras:** **D1** fica difícil de enforçar mecanicamente; reuso de invariantes (`Money`, `Entry`) tende a duplicar; sem layer, architecture tests viram convenções textuais.

### Opção D — Anemic Domain + Service Layer
- Entities só com getters/setters; lógica em `EntryService`.
- **Contras:** anti-pattern explícito de Fowler/Evans; invariantes ficam dispersas; sem ganho para o desafio.

## Decisão

Escolhemos a **Opção B — Clean Architecture** com a estrutura:

```
Cashflow.Ledger/
├── Cashflow.Ledger.Domain          # Money, Entry, EntryType, EntryStatus, domain events
├── Cashflow.Ledger.Application     # MediatR (commands/queries/handlers), validators, behaviors
├── Cashflow.Ledger.Infrastructure  # LedgerDbContext, EF configurations, repositories, MassTransit setup
└── Cashflow.Ledger.Api             # Program.cs, endpoints, auth, healthchecks, swagger
```

**Regras (enforçadas em `tests/Cashflow.ArchitectureTests`):**

1. `Domain` não referencia `EntityFrameworkCore`, `MediatR`, `MassTransit`, `Microsoft.AspNetCore.*`.
2. `Application` referencia `Domain` + `SharedKernel`; **não** referencia `Infrastructure`.
3. `Api` não referencia `Infrastructure` diretamente — apenas via extension `AddLedgerInfrastructure(...)`.
4. `Infrastructure` implementa interfaces declaradas em `Domain`/`Application`.

**DDD-lite aplicado:** `Entry` é agregado com invariantes (`Money.Add`, `Reverse(reason)`, `EntryStatus` state machine). Sem repositórios genéricos: `IEntryRepository` expõe métodos por intenção (`GetByIdAsync`, `AddAsync`, `ListAsync`).

## Consequências

### Positivas
- Domain (`Money`, `Entry`) testado em < 50 ms sem container.
- Layering violado = build vermelho — feedback imediato.
- Invariantes ficam no agregado (cobre **Anemic Domain** anti-pattern).
- Estrutura óbvia para qualquer dev .NET (Clean Arch é canônica).

### Negativas / Trade-offs aceitos
- **4 projetos por BC** — mais arquivos, mais boilerplate em refs.
- **Mais cerimônia** que VSA para features simples (mas cada handler é pequeno).
- **Curva** para entender o fluxo do request: Endpoint → Handler → Repository → DbContext.

### Riscos e mitigações
| Risco | Probabilidade | Impacto | Mitigação |
|---|---|---|---|
| Tentação de injetar `IServiceProvider` em handler para "encurtar" | média | alto | Architecture test proíbe `IServiceProvider` em `Application`; revisão de PR |
| Architecture tests viram ruído | média | alto | Mantidos curtos e focados em layering; falha bloqueia merge |
| Premature interface (1 impl) | alta | médio | F7.1 §1.5 ataca: inline classe quando interface não tem 2º consumidor real |

## Plano de revisão

- Reavaliar se o número de features triplicar (sinal de VSA precisar substituir).
- Métrica de saúde: cobertura de Domain ≥ 90% (line) sem usar mock de framework.

## Referências

- Robert C. Martin, [The Clean Architecture](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html).
- Steve "Ardalis" Smith, [Clean Architecture for ASP.NET Core](https://github.com/ardalis/CleanArchitecture).
- Eric Evans, *Domain-Driven Design* (Blue Book).
- ADRs relacionadas: [ADR-0001](ADR-0001-microsservicos-event-driven.md), [ADR-0002](ADR-0002-dotnet-9-clean-arch.md), [ADR-0013](ADR-0013-test-strategy.md).
