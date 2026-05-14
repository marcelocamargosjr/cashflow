# ADR-0002: .NET 9 + Clean Architecture como base de cada bounded context

- **Status:** Accepted
- **Data:** 2026-05-13
- **Decisores:** @marcelo
- **Tags:** `arquitetura`, `runtime`, `clean-architecture`

## Contexto e problema

Cada bounded context (Ledger, Consolidation) precisa de:
- runtime moderno suportado a longo prazo;
- separação rígida de camadas para testar lógica de negócio sem infraestrutura (Postgres, Mongo, Rabbit, Keycloak);
- code-style enforceável (warnings-as-errors, central package management) compatível com revisão profissional.

A solução deve servir tanto para a Api/Worker quanto para o Gateway YARP.

## Direcionadores da decisão

- **D1.** NFR-M-01 (MUST): Clean Architecture — Domain não depende de Infra.
- **D2.** NFR-M-02 (MUST): architecture tests (NetArchTest) impedem violações.
- **D3.** NFR-M-03 (MUST): `TreatWarningsAsErrors=true` em Release.
- **D4.** Recursos do BCL necessários: `Guid.CreateVersion7` (k-sortable IDs), `DateOnly`/`TimeOnly`, `RateLimiter` nativo, `System.Text.Json` source-generator, `Polly v8.ResiliencePipeline`.
- **D5.** Suporte oficial: queremos um runtime sustentado pela Microsoft pelos próximos ciclos do desafio + portfólio.

## Alternativas consideradas

### Opção A — .NET 8 (LTS atual)
- Maduro, com 3 anos de suporte; equivalente em ASP.NET e EF Core 8.
- **Prós:** LTS oficial até nov/2026; mais documentação consolidada.
- **Contras:** sem `Guid.CreateVersion7` no BCL (precisaria lib externa); rate-limiter menos polido; algumas otimizações de `System.Text.Json` source-gen ainda só no 9.

### Opção B — .NET 9 (STS)
- Release Nov/2024, suporte 18 meses (até maio/2026 — ainda dentro da janela do projeto).
- **Prós:** todos os recursos de **D4** nativos; AOT mais maduro; ASP.NET com hybrid cache + rate limiter melhorado.
- **Contras:** STS (não LTS) — exige plano de upgrade para .NET 10 LTS em nov/2025 (.NET 10 LTS sai com a mesma família de APIs).

### Opção C — .NET Framework 4.8
- Descartado de saída (sem suporte a .NET Standard moderno, sem `DateOnly`, sem Minimal API).

### Opção D — Java/Spring Boot
- Stack maduro e equivalente em recursos.
- **Contras:** não casa com o restante do portfólio do autor; ferramental .NET tem maior fluência aqui; sem ganho técnico real para o desafio.

## Decisão

Escolhemos **Opção B — .NET 9** com **Clean Architecture** (Domain → Application → Infrastructure/Api).

**Layering enforced via architecture tests** ([ADR-0003](ADR-0003-clean-architecture.md)) e `dotnet test tests/Cashflow.ArchitectureTests`:
- `Cashflow.Ledger.Domain` não referencia `EntityFrameworkCore`, `MediatR`, `MassTransit`, `Microsoft.AspNetCore.*`.
- `Cashflow.Ledger.Application` referencia `Domain` e `SharedKernel`, mas não `Infrastructure`.
- `Cashflow.Ledger.Api` não referencia `Infrastructure` diretamente — apenas via extension `AddInfrastructure(IServiceCollection)`.

**Razões principais:**
- **D4** decide — `Guid.CreateVersion7` (ULID-like k-sortable, melhor locality em índices Postgres) é o desempate técnico imediato sem dependência de NuGet externa.
- **D2/D3** são satisfeitas pelos analyzers já incluídos (`AnalysisLevel=latest-recommended` + `EnforceCodeStyleInBuild=true`) — configurados em `Directory.Build.props`.
- Plano de upgrade para .NET 10 LTS (nov/2025) é incremental — todas as APIs usadas têm continuidade declarada.

## Consequências

### Positivas
- Lógica de negócio (Domain) roda em testes unit em milissegundos sem Docker.
- Architecture tests bloqueiam regressão de layering no CI.
- Stack moderna e idiomática, demonstrando familiaridade com práticas atuais.
- Central Package Management (`Directory.Packages.props`) reduz drift de versões entre projetos.

### Negativas / Trade-offs aceitos
- **STS, não LTS** — janela de upgrade obrigatória para .NET 10 antes de maio/2026.
- **Mais cerimônia** que um single-project — 4 projetos por bounded context (Domain, Application, Infrastructure, Api).
- **Curva inicial** para devs sem familiaridade com Clean Arch + MediatR.

### Riscos e mitigações
| Risco | Probabilidade | Impacto | Mitigação |
|---|---|---|---|
| Upgrade .NET 10 quebra APIs | baixa | médio | CI roda contra `9.0.x`; upgrade planejado em milestone separado |
| Architecture tests viram "ruído" e são desabilitados | média | alto | Tests rodam em todo PR; falha bloqueia merge |
| Hot reload / debug mais lento por causa do layering | baixa | baixo | Tooling do VS/Rider lida bem; perf irrelevante em prod |

## Plano de revisão

- Reavaliar em **out/2025** (com .NET 10 LTS preview disponível).
- Métrica de saúde: tempo de build CI (< 4 min full) e coverage Domain ≥ 90%.

## Referências

- [.NET 9 Release notes](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-9/overview)
- [.NET support policy](https://dotnet.microsoft.com/platform/support/policy/dotnet-core)
- Mark Seemann, *Dependency Injection in .NET* (3rd ed.).
- ADRs relacionadas: [ADR-0001](ADR-0001-microsservicos-event-driven.md), [ADR-0003](ADR-0003-clean-architecture.md).
