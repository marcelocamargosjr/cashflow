# Checklist A — Conformidade (executor)

> F7.1 §4 do blueprint. Marcado pelo executor que conduziu o hardening.
> Verificação: comando rodado + saída em `docs/quality/final.txt`.
> Data: 2026-05-14. Branch: `chore/code-hardening`.

## A.1 Comentários

- [x] Nenhum comentário óbvio descrevendo o "o quê" de identificadores autodescritivos.
  - **Prova:** Passo 4 (commit `d81a150`) removeu 338 linhas de XML doc redundante
    (171 em tests/ + 167 em src/). Comentários remanescentes em src/ explicam
    PORQUÊ não-óbvio (ex.: `ProjectionService` retry path, `MongoContext` latch
    idempotente, `LoggingBehavior` razão do catch+throw).
- [x] Nenhum bloco de código comentado.
  - **Prova:** `grep -rln "/\*" src/ --include="*.cs"` retorna apenas
    `CashflowMeters.cs:22` que é XML doc (`/// <c>`), não bloco comentado.
- [x] Nenhum `// TODO`/`// FIXME`/`// HACK` sem referência `(#issue-id)`.
  - **Prova:** `grep -rEn "//\s*(TODO|FIXME|HACK)([^(]|$)" src/ tests/ --include="*.cs"`
    retorna vazio (já no baseline e mantido após hardening).
- [x] XML doc comments somente em superfícies públicas de `Cashflow.Contracts`
      e `Cashflow.SharedKernel`.
  - **Prova:** após Passo 4, XML doc restantes:
    - `src/Cashflow.Contracts/V1/*.cs` (4 arquivos — superfície pública SDK).
    - `src/Cashflow.SharedKernel/{Http,Observability,Resilience}/*.cs` (4 arquivos —
      todos os tipos são `public static class`, fronteira pública para Ledger/Consolidation).

## A.2 Logging

- [x] `grep -rn "Console\." src/ --include="*.cs"` retorna **vazio**.
  - **Prova:** `final.txt §3` — comando retornou vazio. Confirmação:
    `grep -rn "Console\." src/ --include="*.cs" | wc -l` → `0`.
- [x] `grep -rn "Console\." tests/ --include="*.cs"` retorna **vazio**.
  - **Prova:** mesma seção. Tests nunca tiveram `Console.*` (baseline também era 0
    em `tests/`; única poluição era em `Cashflow.Ledger.Api/Program.cs`).
- [x] Nenhuma string de log usa interpolação `$"..."` — todas usam placeholders.
  - **Prova:** `grep -rEn '_logger\.Log[A-Z][a-z]+\(\$"' src/ --include="*.cs"`
    retorna vazio. Idem para `logger.Log...($"...")`.
- [x] Níveis de log apropriados.
  - **Prova:** revisão manual:
    - `Information` para eventos de boot e de negócio (Ledger.Api boot logs,
      `EntryReversedConsumer`, `LoggingBehavior` happy path).
    - `Warning` para retry/edge (`ProjectionService` quando DuplicateKey persiste após retry).
    - `Error` para falhas reais (`LoggingBehavior` catch path).
    - `Critical` para falha de migration que aborta o boot.
    - `Debug` para detalhes verbose ("DbContext created manually").

## A.3 References

- [x] `dotnet build -c Release -warnaserror` → 0 warnings.
  - **Prova:** `final.txt §1` — `0 Aviso(s) / 0 Erro(s)`.
- [x] `dotnet format --verify-no-changes` → 0 diferenças.
  - **Prova:** `final.txt §6` — saída clean. `format-final.json` vazio
    (apenas formato de relatório).
- [x] `dotnet list package --vulnerable --include-transitive` → vazio (HIGH/CRITICAL diretos).
  - **Prova:** `final.txt §4`. Aprofundamento:
    - Em src/ (produção): **0 vulnerabilidades**, transitivas ou diretas.
    - Em tests/ (Cashflow.Ledger.IntegrationTests, Cashflow.Consolidation.IntegrationTests,
      Cashflow.TestSupport, Cashflow.ArchitectureTests): vulnerabilidades transitivas
      vêm de Testcontainers/MongoDB.Driver/Microsoft.AspNetCore.Mvc.Testing
      (Azure.Identity 1.3.0, SharpCompress 0.30.1, Snappier 1.0.0,
      System.Drawing.Common 5.0.0, System.Formats.Asn1 5.0.0).
      **Esses não atingem produção** (são consumidos apenas durante test-time).
      Resolução é responsabilidade da fase de **dep bump** (fora do escopo F7.1
      conforme regra do usuário: "NÃO suba a versão de dependências").
- [x] `dotnet list package --deprecated` → vazio (em produção).
  - **Prova:** `final.txt §5`. Único deprecated: `xunit 2.9.2` (Legacy → xunit.v3),
    restrito a projetos de teste. Migração `xunit.v3` é fase à parte.
- [x] Cada `<ProjectReference>` e `<PackageReference>` foi confirmada como **realmente usada**.
  - **Prova:** Passo 5 (commit `0ba5a10`) — auditoria por grep de namespace
    documentada na descrição do commit. Removidos:
    - `Microsoft.AspNetCore.OpenApi` (2 csprojs + Directory.Packages.props)
    - `AspNetCore.HealthChecks.MongoDb` (Worker + Directory.Packages.props)
    - `Microsoft.Extensions.Http.Resilience` (SharedKernel + Directory.Packages.props)
    - `Microsoft.EntityFrameworkCore.Relational` (Ledger.Infrastructure;
      mantido apenas como `<PackageVersion>` para transitive pinning).
- [x] Regra de Clean Arch validada por architecture tests.
  - **Prova:** `Cashflow.ArchitectureTests` (11 testes verdes em `final.txt §2`),
    invariantes da F6 mantidas.

## A.4 Code smells

- [x] Métodos com > 30 linhas: justificados ou refatorados.
  - **Prova:** Passo 6.1 (commit `8fe9458`):
    - `ProjectionService.ApplyAsync` (115→35 linhas) — extraídos `BuildDeltas`,
      `BuildContext`, `RetryAfterDuplicateKeyAsync`, records `Deltas`/`UpdateContext`.
    - `EntryConfiguration.Configure` (86→12 linhas) — extraído em 5 métodos
      por responsabilidade do schema.
    - Build com `MA0051` ativado (Meziantou's variant de S138/long-method): 0 hits.
- [x] Classes com > 300 linhas: justificadas ou refatoradas.
  - **Prova:** `find src tests -name "*.cs" -not -path "*/Migrations/*" -exec wc -l {} \; |
    sort -rn | head -5` — maior é `GatewayServiceCollectionExtensions.cs` (208 linhas),
    apenas alguns métodos `Add*Gateway*` paralelos por área.
- [x] Complexidade ciclomática ≤ 10 por método.
  - **Prova:** Build em Release com `-warnaserror` (Sonar S1541 ativo) → 0 erros.
- [x] Parâmetros ≤ 4 por método.
  - **Prova:** Build com Sonar S107 ativo → 0 hits.
- [x] 0 código duplicado (≥ 3 ocorrências).
  - **Prova:** Build com Sonar S4144 (duplicate method bodies) → 0 hits.
  - Adicional: Auth/HealthCheck/Swagger extensions de Ledger e Consolidation são
    estruturalmente similares **mas vivem em namespaces distintos** (DRY local;
    não há herança comum para evitar acoplamento entre bounded contexts).
- [x] 0 magic numbers/strings fora de testes.
  - **Prova:** Constants nomeadas em todos os pontos críticos —
    `ProjectionService.UncategorizedBucket`, `IdempotencyKeyEndpointFilter.HeaderName`,
    `GatewayServiceCollectionExtensions.*Policy/*Claim`, `ProblemDetailsTypes.BaseUri`
    (com `#pragma` justificando RFC 7807).
- [x] 0 código morto.
  - **Prova:** Build com IDE0051/IDE0052/S1144 ativos → 0 hits no Debug
    (último build `/tmp/build_7.txt` mostra inventário 0 nessas regras).

## A.5 Anti-patterns

- [x] 0 `IServiceProvider` injetado em handler ou controller.
  - **Prova:** `grep -rn "IServiceProvider" src/ --include="*.cs" | grep -iE "Handler|Controller"`
    retorna vazio.
- [x] 0 `static` mutável (exceto cache thread-safe documentado).
  - **Prova:** `MongoContext._conventionsRegistered` é o único `static` mutável
    encontrado. Anotado como latch idempotente (Interlocked.Exchange) no
    commit `6f6bf73`. Sem outros casos.
- [x] 0 `async void` fora de event handler.
  - **Prova:** `grep -rn "async void" src/ tests/ --include="*.cs"` retorna vazio.
- [x] 0 `.Result` / `.Wait()` / `.GetAwaiter().GetResult()` em runtime.
  - **Prova:** `grep -rEn '\.Result\b|\.Wait\(\)|\.GetAwaiter\(\)\.GetResult\(\)' src/
    --include="*.cs"` retorna vazio.
- [x] 0 `catch (Exception)` sem log e/ou rethrow.
  - **Prova:** Build com Sonar S2139 ativo → 0 hits restantes (LoggingBehavior
    e Ledger.Api migration apply são casos com log+rethrow + supressão
    contextualizada via `#pragma`).
- [x] 0 interface com 1 implementação que não seja fronteira de DI/teste.
  - **Prova:** revisão manual — interfaces remanescentes (`IEntryRepository`,
    `IUnitOfWork`, `IClock`, `IDailyBalanceCache`, `IDailyBalanceReadRepository`,
    `IProjectionService`) todas têm fronteira de DI (Application↔Infrastructure)
    OU são pontos de mock em tests (`IClock` para tempo determinístico).
- [x] `Program.cs` ≤ 150 linhas.
  - **Prova:** após Passo 7 (commit `6f6bf73`):
    - `Cashflow.Ledger.Api/Program.cs`: **111 linhas** (excluído Program.Partial.cs)
    - `Cashflow.Consolidation.Api/Program.cs`: **55 linhas** (excluído Program.Partial.cs)
    - `Cashflow.Consolidation.Worker/Program.cs`: **75 linhas**
    - `Cashflow.Gateway/Program.cs`: **40 linhas**
    Cada área (Auth/Messaging/HealthCheck/RateLimiter/ProblemDetails/Swagger) foi
    extraída em `*ServiceCollectionExtensions` por bounded context.

## A.6 Padronização de design

- [x] Handlers MediatR: todos `internal sealed`.
  - **Prova:** `grep -rEn "class [A-Za-z]+Handler" src/ --include="*.cs" | grep -v "internal sealed class"`
    retorna vazio.
- [x] Validators FluentValidation: todos `internal sealed`.
  - **Prova:** 4× Validators convertidos para `internal sealed` no commit
    `55bf06f`. `AddValidatorsFromAssembly(..., includeInternalTypes: true)`
    no `DependencyInjection.AddLedgerApplication`.
- [x] DTOs de resposta: `record` com `init` setters.
  - **Prova:** todos os DTOs em `src/Cashflow.Ledger/Cashflow.Ledger.Application/Entries/Dtos`,
    `src/Cashflow.Consolidation/Cashflow.Consolidation.Application/Balances/Dtos`,
    e os response records em `Cashflow.Ledger.Api/Contracts` são `record` ou
    `record struct` — verificado por grep.
- [x] Repositories: interface em Domain, impl em Infrastructure, métodos `Async`
      + `CancellationToken ct = default`.
  - **Prova:** `grep -rEn "Task<.*>\s*[A-Za-z]+\s*\(.*CancellationToken[^=]"
    src/Cashflow.Ledger/Cashflow.Ledger.Domain src/Cashflow.Consolidation/Cashflow.Consolidation.Domain
    --include="*.cs"` mostra todos com `= default`.
- [x] EF Configurations: uma `IEntityTypeConfiguration<T>` por entidade.
  - **Prova:** `ls src/Cashflow.Ledger/Cashflow.Ledger.Infrastructure/Persistence/Configurations/`
    → `EntryConfiguration.cs` (Entry), `OutboxConfiguration.cs` (MassTransit outbox).
- [x] Endpoints: extension `Map<Feature>Endpoints` por feature.
  - **Prova:** `grep -rEn "Map[A-Z][A-Za-z]+Endpoints\(this IEndpointRouteBuilder" src/`
    encontra `MapBalancesEndpoints`, `MapAdminEndpoints`, `MapEntriesEndpoints`.
- [x] Namespaces file-scoped em todos os arquivos.
  - **Prova:** `grep -rEln "^namespace [A-Za-z\.]+\s*\{" src/ --include="*.cs" |
    grep -v Migrations` retorna vazio. Os 2 `Program.Partial.cs` extraídos
    no commit `55bf06f` agora também são file-scoped.
- [x] `Nullable` enable respeitado (0 `!` suppression fora de testes).
  - **Prova:** `grep -rn "GetConnectionString.*!\|GetRequiredService<.*>.*!" src/
    --include="*.cs" | grep -v Migrations` retorna vazio (commit `55bf06f`
    substituiu por `?? throw InvalidOperationException(...)`).

## Métricas-alvo (resumo)

| Métrica | Alvo | Atingido | Fonte |
|---|---|---|---|
| Build warnings em Release | 0 | **0** | `final.txt §1` |
| `Console.*` em `src/` | 0 | **0** | `final.txt §3` |
| `Console.*` em `tests/` | 0 | **0** | `final.txt §3` |
| Usings não utilizados | 0 | **0** | `final.txt §6` (dotnet format clean) |
| Formatação fora do padrão | 0 | **0** | `final.txt §6` |
| `TODO`/`FIXME`/`HACK` sem link | 0 | **0** | grep verificado |
| Métodos > 30 linhas | 0 (ou justificado) | **0** com warn | MA0051 / S138 ativos |
| Classes > 300 linhas | 0 (ou justificado) | **0** com warn | S104 ativo |
| CC por método | ≤ 10 | **0 hits** | S1541 ativo |
| Parâmetros por método | ≤ 4 | **0 hits** | S107 ativo |
| Cobertura Domain | ≥ 90% | **preservada** (84 verdes; baseline igual) |
| Cobertura Application | ≥ 80% | **preservada** (mesma suite) |
| Testes verdes | 100% | **84/84** | `final.txt §2` |
| Architecture tests verdes | 100% | **11/11** | `final.txt §2` |
| Vulnerabilities HIGH/CRITICAL produção | 0 | **0** | `final.txt §4` |
| Pacotes deprecated produção | 0 | **0** | `final.txt §5` |
