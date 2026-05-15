#!/usr/bin/env bash
# =============================================================================
# make-perf.sh — `08-TESTES.md §6.3`.
#
# Obtém token Keycloak no client `cashflow-api` — default = `password` grant
# (user `merchant1@cashflow.local`); alternável para `client_credentials` via
# `GRANT_TYPE=client_credentials`. Dispara o cenário k6 (`balance-50rps.js` por
# padrão) e salva o output em `docs/performance/k6-result-YYYY-MM-DD.json` como
# evidência do NFR.
#
# Uso:
#   ./scripts/make-perf.sh                 # cenário NFR (50 req/s × 60s)
#   SCRIPT=balance-smoke.js ./scripts/make-perf.sh
# Env vars sobrescrevíveis: KEYCLOAK, CLIENT_ID, CLIENT_SECRET, MERCHANT_ID,
#                           TARGET_DATE, COMPOSE_FILE.
#
# Compatibilidade Windows (Git Bash / MSYS2):
#   Git Bash reescreve argumentos que parecem caminhos Unix (ex.:
#   `/scripts/balance-50rps.js`) para caminhos Windows (`C:/Program Files/Git/
#   scripts/...`). Isso quebra o `k6 run /scripts/...` dentro do container.
#   Este script desativa a conversão exportando MSYS_NO_PATHCONV=1 e
#   MSYS2_ARG_CONV_EXCL=* — inofensivo em Linux/macOS.
# =============================================================================
set -euo pipefail
export MSYS_NO_PATHCONV=1
export MSYS2_ARG_CONV_EXCL='*'

# --- defaults --------------------------------------------------------------
KEYCLOAK="${KEYCLOAK:-http://localhost:8080}"
REALM="${REALM:-cashflow}"
CLIENT_ID="${CLIENT_ID:-cashflow-api}"
CLIENT_SECRET="${CLIENT_SECRET:-cashflow-api-secret}"
MERCHANT_ID="${MERCHANT_ID:-0193e7a8-d8f0-7c5e-9b21-3f9f8a4d1c00}"
TARGET_DATE="${TARGET_DATE:-2026-05-13}"
SCRIPT="${SCRIPT:-balance-50rps.js}"
BASE_URL="${BASE_URL:-http://gateway:8080}"
COMPOSE_FILE="${COMPOSE_FILE:-infra/docker-compose.yml}"

# --- deps ------------------------------------------------------------------
for bin in curl jq docker; do
  command -v "$bin" >/dev/null 2>&1 || { echo >&2 "missing dep: $bin"; exit 127; }
done

today="$(date -u +%Y-%m-%d)"
out_host_dir="${PWD}/docs/performance"
out_host_file="${out_host_dir}/k6-result-${today}.json"
mkdir -p "$out_host_dir"

# --- token Keycloak --------------------------------------------------------
# Por padrão usamos `password` grant para `merchant1@cashflow.local` — o realm
# seed (07 §3.1.2) traz esse usuário com role `merchant` + attribute `merchantId`,
# que é o que a policy `RequireMerchant` e o handler resource-based exigem.
#
# Para alternar para `client_credentials` (puro), exporte GRANT_TYPE=client_credentials
# (requer ajustar o service-account user em Keycloak para ter merchantId+merchant role).
# Variáveis com prefixo KC_ para não colidir com USERNAME/PASSWORD que o Git Bash
# já herda no Windows (=usuário do SO).
GRANT_TYPE="${GRANT_TYPE:-password}"
KC_USERNAME="${KC_USERNAME:-merchant1@cashflow.local}"
KC_PASSWORD="${KC_PASSWORD:-merchant123}"

echo ">>> Obtendo token Keycloak (${GRANT_TYPE})"
if [[ "$GRANT_TYPE" == "password" ]]; then
  TOKEN_RESPONSE="$(curl -fsS -X POST \
    "${KEYCLOAK}/realms/${REALM}/protocol/openid-connect/token" \
    -H 'Content-Type: application/x-www-form-urlencoded' \
    --data-urlencode "grant_type=password" \
    --data-urlencode "client_id=${CLIENT_ID}" \
    --data-urlencode "client_secret=${CLIENT_SECRET}" \
    --data-urlencode "username=${KC_USERNAME}" \
    --data-urlencode "password=${KC_PASSWORD}" \
    --data-urlencode "scope=openid")"
else
  TOKEN_RESPONSE="$(curl -fsS -X POST \
    "${KEYCLOAK}/realms/${REALM}/protocol/openid-connect/token" \
    -H 'Content-Type: application/x-www-form-urlencoded' \
    --data-urlencode "grant_type=client_credentials" \
    --data-urlencode "client_id=${CLIENT_ID}" \
    --data-urlencode "client_secret=${CLIENT_SECRET}")"
fi

TOKEN="$(printf '%s' "$TOKEN_RESPONSE" | jq -r '.access_token // empty')"
if [[ -z "$TOKEN" ]]; then
  echo >&2 "Falha ao obter access_token. Resposta:"
  echo >&2 "$TOKEN_RESPONSE"
  exit 1
fi
echo "    Token obtido ($(printf '%s' "$TOKEN" | wc -c) bytes)"

# --- corre k6 --------------------------------------------------------------
echo ">>> Disparando k6: ${SCRIPT}"
docker compose -f "${COMPOSE_FILE}" --profile perf run --rm \
  -e BASE_URL="${BASE_URL}" \
  -e TOKEN="${TOKEN}" \
  -e MERCHANT_ID="${MERCHANT_ID}" \
  -e TARGET_DATE="${TARGET_DATE}" \
  -v "${out_host_dir}:/out" \
  k6 run \
    --summary-export=/out/k6-result-${today}.json \
    "/scripts/${SCRIPT}"

rc=$?
if [[ $rc -ne 0 ]]; then
  echo >&2 ">>> k6 falhou com exit code ${rc} — threshold NFR provavelmente violado."
  exit "$rc"
fi

echo ">>> OK. Resultado salvo em ${out_host_file}"
