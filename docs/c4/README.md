# Diagramas C4 — Cashflow

Diagramas no padrão **C4 model** (Brown) renderizados em **Mermaid** (compatível com o preview do GitHub).

| Nível | Diagrama | Foco |
|---|---|---|
| 1 — Context | [context.mmd](context.mmd) | Atores externos (merchant, admin) e sistemas vizinhos (Keycloak, Grafana, Edge TLS) |
| 2 — Containers | [containers.mmd](containers.mmd) | Cashflow.Gateway / Ledger.Api / Consolidation.Api / Worker + bancos / broker / IdP / OTel |
| 3 — Components (Ledger) | [components-ledger.mmd](components-ledger.mmd) | Decomposição do Ledger: Endpoints / Handlers MediatR / Domain / Infrastructure |
| 3 — Components (Consolidation) | [components-consolidation.mmd](components-consolidation.mmd) | Decomposição da Consolidation: Endpoints / Handlers / ProjectionService / Cache / Worker |

## Como visualizar

- **GitHub:** os arquivos `.mmd` renderizam automaticamente como diagramas Mermaid nas previews de markdown (basta abrir o arquivo na UI do GitHub).
- **Localmente (VS Code):** instale a extensão *Markdown Preview Mermaid Support* e abra qualquer `.mmd` com Ctrl+Shift+V.
- **Mermaid Live Editor:** https://mermaid.live → cole o conteúdo do `.mmd` (sem o front-matter `---`) para validar.
- **Export para PNG/SVG:** `mmdc -i context.mmd -o context.png` (usa o pacote npm `@mermaid-js/mermaid-cli`).

## Convenções

- Cada diagrama tem `title` no front-matter Mermaid + `title` no diagrama (redundância intencional para tooling externo).
- Bibliotecas Mermaid C4: `C4Context`, `C4Container`, `C4Component`. Sintaxe documentada em https://mermaid.js.org/syntax/c4.html.
- `UpdateLayoutConfig` no fim de cada diagrama controla o layout (linhas por row).
- ContainerDb / SystemDb para bancos; Container_Ext / System_Ext para sistemas fora do nosso boundary.
