# ADR-XXXX: [Título conciso da decisão]

> **Template MADR (Markdown Architectural Decision Records).** Copie este arquivo, renomeie para `ADR-XXXX-titulo-curto.md`, e preencha. Mantenha em `docs/adr/`.

- **Status:** Proposed | Accepted | Deprecated | Superseded by [ADR-YYYY](ADR-YYYY-link.md)
- **Data:** YYYY-MM-DD
- **Decisores:** @marcelo (e revisores se houver)
- **Tags:** `arquitetura`, `dominio`, `infra`, `seguranca`, etc.

## Contexto e problema

Descreva o problema que motivou a decisão. Cite:
- forças em jogo (NFR, custos, prazo);
- restrições técnicas e de negócio;
- alternativas que já foram descartadas antes desta análise.

## Direcionadores da decisão

- **D1.** ...
- **D2.** ...
- **D3.** ...

(use os requisitos não-funcionais do `02-NFR-E-ACEITE.md` como base quando aplicável.)

## Alternativas consideradas

### Opção A — [Nome]
- Descrição em 2-3 linhas.
- **Prós:** ...
- **Contras:** ...

### Opção B — [Nome]
- Descrição.
- **Prós:** ...
- **Contras:** ...

### Opção C — [Nome]
- Descrição.
- **Prós:** ...
- **Contras:** ...

## Decisão

Escolhemos **Opção X** porque ...

(seja explícito; cite os direcionadores que pesaram mais.)

## Consequências

### Positivas
- ...
- ...

### Negativas / Trade-offs aceitos
- ...
- ...

### Riscos e mitigações
| Risco | Probabilidade | Impacto | Mitigação |
|---|---|---|---|
| ... | baixa/média/alta | baixo/médio/alto | ... |

## Plano de revisão

- Quando reavaliar: [evento, ex.: ao atingir 10x o volume atual, ou em 6 meses].
- Métrica de saúde: [métrica observável que indica se a decisão segue válida].

## Referências

- [Link 1]
- [Link 2]
- ADRs relacionadas: [ADR-YYYY](ADR-YYYY-...)
