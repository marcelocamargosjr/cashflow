# OpenAPI & Event Schemas — Cashflow

Contratos públicos exportados como **OpenAPI 3.0.3** (HTTP) e **JSON Schema 2020-12** (eventos AMQP).

## HTTP APIs

| Serviço | Spec | Endpoint |
|---|---|---|
| Ledger (write-side) | [ledger.v1.yaml](ledger.v1.yaml) | `POST/GET /ledger/api/v1/entries`, reverse, list, get |
| Consolidation (read-side) | [consolidation.v1.yaml](consolidation.v1.yaml) | `GET /consolidation/api/v1/balances/{merchantId}/{daily\|period\|current}` |

### Como validar

- **Online:** abra https://editor.swagger.io e cole o YAML — o editor faz lint + render.
- **CLI:**
  ```bash
  npx @redocly/cli@latest lint docs/openapi/ledger.v1.yaml
  npx @redocly/cli@latest lint docs/openapi/consolidation.v1.yaml
  ```
- **Local (dev):** `http://localhost:8001/swagger` (Ledger) e `http://localhost:8002/swagger` (Consolidation) servem Swashbuckle UI baseado na mesma metadata.

### Como regenerar a partir do código

Após mudanças nos endpoints/DTOs, exportar via Swashbuckle CLI:

```bash
dotnet build -c Release src/Cashflow.Ledger/Cashflow.Ledger.Api
dotnet tool install -g Swashbuckle.AspNetCore.Cli   # uma vez
dotnet swagger tofile --yaml \
  --output docs/openapi/ledger.v1.yaml \
  src/Cashflow.Ledger/Cashflow.Ledger.Api/bin/Release/net9.0/Cashflow.Ledger.Api.dll \
  v1
dotnet swagger tofile --yaml \
  --output docs/openapi/consolidation.v1.yaml \
  src/Cashflow.Consolidation/Cashflow.Consolidation.Api/bin/Release/net9.0/Cashflow.Consolidation.Api.dll \
  v1
```

Os YAMLs **commitados** neste diretório são a fonte de verdade externa para consumers (frontend, postman collections, contract tests). PRs que alteram endpoints **devem** atualizar o YAML correspondente.

## Eventos AMQP

| Evento | Schema | Origem |
|---|---|---|
| `Cashflow.Contracts.V1:EntryRegisteredV1` | [events/EntryRegistered.v1.schema.json](events/EntryRegistered.v1.schema.json) | `src/Cashflow.Contracts/V1/EntryRegisteredV1.cs` |
| `Cashflow.Contracts.V1:EntryReversedV1` | [events/EntryReversed.v1.schema.json](events/EntryReversed.v1.schema.json) | `src/Cashflow.Contracts/V1/EntryReversedV1.cs` |

### Versionamento

- `V1`, `V2` em **namespaces .NET separados** (`Cashflow.Contracts.V1.EntryRegisteredV1` vs `Cashflow.Contracts.V2.EntryRegisteredV2`).
- Consumers escolhem qual versão consumir; broker mantém ambas em paralelo durante a transição.
- Breaking change **nunca** muda `V1` em vigor — sempre incrementa.

### Validação JSON Schema

```bash
npx ajv-cli@latest validate \
  -s docs/openapi/events/EntryRegistered.v1.schema.json \
  -d examples.json
```

Os exemplos no campo `examples` do schema são validados pelos integration tests.
