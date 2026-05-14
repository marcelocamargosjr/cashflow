<!--
PR template — branch protection em `main` requer este checklist preenchido,
1 review aprovado, conversations resolvidas e o status check `CI` verde.
-->

## Resumo

<!-- O que mudou e por quê (em ~3 linhas). Não repita o changelog. -->

## Tipo de mudança

- [ ] `feat` — nova funcionalidade
- [ ] `fix` — correção de bug
- [ ] `refactor` — sem mudança de comportamento externo
- [ ] `perf` — ganho de performance
- [ ] `docs` — só documentação
- [ ] `test` — só testes
- [ ] `chore` / `ci` / `build` — infra do repo
- [ ] `breaking` — mudança incompatível (requer bump major)

## Issue / contexto

Closes #
Relacionado: #

## Como testar

```bash
# Comando(s) para validar localmente.
make up && make test
# ...
```

## Checklist obrigatório

- [ ] **Build** local verde (`dotnet build -c Release /warnaserror`).
- [ ] **Testes** unitários, arquiteturais e de integração passando (`make test`).
- [ ] **Cobertura** ≥ 90% Domain / ≥ 80% Application (gate do CI — `08-TESTES §7`).
- [ ] **Sem segredos** versionados (`.env` continua no `.gitignore`).
- [ ] **OWASP** revisado (`07-INFRA §3.4`) — se aplicável.
- [ ] **LGPD** revisado (`docs/lgpd.md`) — se manipula dados pessoais.

## Documentação

- [ ] README atualizado (se UX/runtime mudou).
- [ ] OpenAPI atualizado em `docs/openapi/*` (se endpoints mudaram).
- [ ] Runbook atualizado em `docs/runbook.md` (se operação mudou).
- [ ] Diagramas C4 / sequência atualizados (se topologia/fluxo mudou).

## ADR

- [ ] Não aplicável (mudança não-arquitetural).
- [ ] ADR criado em `docs/adr/ADR-NNNN-<slug>.md` (formato MADR).
- [ ] ADR existente atualizado (citar qual).

## Contratos públicos

- [ ] Não altero contratos.
- [ ] Mudança **retrocompatível** (campo opcional, default seguro).
- [ ] Mudança **incompatível** → criada nova versão de evento `*.v2` / endpoint `/v2/...` e plano de migração documentado.

## Observabilidade

- [ ] Não introduzo I/O / fluxo novo.
- [ ] Métricas custom adicionadas em `Cashflow.*` (atualizar `07-INFRA §2.5`).
- [ ] Logs respeitam minimização (sem PII em mensagem livre).
- [ ] Spans nomeados conforme convenção `Cashflow.<Component>.<Operation>`.

## Riscos / rollback

<!-- O que pode dar errado? Plano de rollback (revert? feature flag? migration reversa?). -->

## Screenshots / evidências

<!-- Se UI mudou; se NFR foi revalidado (`make perf`), cole o snippet do summary do k6. -->
