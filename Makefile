# Cashflow — alvos cross-platform (07 §6).
# No Windows: usar `scripts/make-up.ps1` (mesma semântica via PowerShell).

SHELL := /usr/bin/env bash
COMPOSE := docker compose -f infra/docker-compose.yml --env-file infra/.env
# `down`/`nuke` precisam dos profiles ativados — services profile-gated não são
# afetados por `docker compose down` sem `--profile` correspondente.
COMPOSE_ALL := $(COMPOSE) --profile core --profile app --profile tools --profile perf

.PHONY: up up-core up-tools down nuke logs seed perf chaos restore chaos-validate test build

# `make up` sobe core + app — F5 §Checkpoint requer ambos. NÃO use `--profile app`
# sozinho: services do profile core não sobem por dependência transitiva quando
# estão em outro profile inativo (caveat documentado no prompt F5).
up:
	$(COMPOSE) --profile core --profile app up -d

up-core:
	$(COMPOSE) --profile core up -d

up-tools:
	$(COMPOSE) --profile core --profile tools up -d

down:
	$(COMPOSE_ALL) down --remove-orphans

# CUIDADO: `nuke` apaga volumes (dados Postgres/Mongo/Rabbit/Redis/Keycloak).
nuke:
	$(COMPOSE_ALL) down -v --remove-orphans

logs:
	$(COMPOSE) logs -f $(SERVICE)

seed:
	@curl -sS -X POST http://localhost:8000/ledger/admin/seed \
	  -H "Authorization: Bearer $$TOKEN" \
	  -H "Content-Type: application/json" \
	  -d '{"days": 30, "entriesPerDay": 20}'

# `perf` delega ao script que (1) busca token Keycloak via client_credentials,
# (2) injeta MERCHANT_ID/TARGET_DATE, (3) roda k6 e (4) salva
# `docs/performance/k6-result-YYYY-MM-DD.json` como evidência do NFR.
perf:
	bash scripts/make-perf.sh

# Chaos atômico (07 §6): apenas para `make chaos && make restore` manual.
# Para a prova automatizada do NFR de isolamento use `make chaos-validate`.
chaos:
	$(COMPOSE) stop consolidation-api consolidation-worker

restore:
	$(COMPOSE) start consolidation-api consolidation-worker

# Prova literal do NFR "Ledger sobrevive a Consolidation down"
# (11-VALIDACAO-REQUISITOS-PDF.md §2.1).
chaos-validate:
	bash scripts/chaos-validate.sh

test:
	dotnet test

build:
	$(COMPOSE) --profile app build
