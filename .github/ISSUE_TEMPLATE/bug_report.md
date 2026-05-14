---
name: Bug report
about: Reportar comportamento inesperado, defeito ou regressão.
title: "bug: <resumo curto>"
labels: ["bug", "triage"]
assignees: []
---

## Descrição

<!-- O que aconteceu? Comportamento atual vs. esperado. -->

## Passos para reproduzir

1.
2.
3.

## Comportamento esperado

<!-- O que deveria ter acontecido? -->

## Comportamento observado

<!-- O que aconteceu de fato? Inclua mensagens de erro, stack traces. -->

## Ambiente

- Branch / commit:
- SO:
- .NET SDK (`dotnet --info`):
- Docker / Compose:
- Como reproduziu (local `make up` / CI / outro):

## Logs / evidências

<details><summary>Logs relevantes</summary>

```text
<cole logs aqui>
```

</details>

## Severidade

- [ ] **Crítico** — produção fora do ar / perda de dados
- [ ] **Alto** — funcionalidade principal quebrada, sem workaround
- [ ] **Médio** — funcionalidade quebrada, com workaround
- [ ] **Baixo** — cosmético / edge case raro

## Componente afetado

- [ ] Ledger.Api
- [ ] Consolidation.Api
- [ ] Consolidation.Worker
- [ ] Gateway (YARP)
- [ ] Infra / docker-compose
- [ ] Observabilidade (OTel / Grafana / Prometheus / Loki / Tempo)
- [ ] Documentação
- [ ] Outro:

## Checklist

- [ ] Reproduzido em ambiente limpo (`make nuke && make up`).
- [ ] Verifiquei se já existe issue parecida.
- [ ] Anexei `correlationId` se disponível (extrai de qualquer span no Tempo / log no Loki).
