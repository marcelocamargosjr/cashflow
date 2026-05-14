#!/usr/bin/env bash
# =============================================================================
# chaos-validate.sh — `11-VALIDACAO-REQUISITOS-PDF.md §2.1`.
#
# Prova o NFR de isolamento do PDF: "lançamento não deve ficar indisponível se
# o consolidado cair".
#
# Passos:
#   1. Para `consolidation-api`, `consolidation-worker` e `mongo` (consolidação
#      completamente offline).
#   2. Dispara 100 POST em /ledger/api/v1/entries — espera 100/100 → 201.
#   3. Restaura os serviços de consolidação.
#   4. Mede catch-up até `entriesCount >= 100` (≤ 60s).
#
# Uso:
#   ./scripts/chaos-validate.sh
# Env vars: GATEWAY, KEYCLOAK, MERCHANT_ID, TARGET_DATE, COMPOSE_FILE.
# =============================================================================
set -euo pipefail

GATEWAY="${GATEWAY:-http://localhost:8000}"
KEYCLOAK="${KEYCLOAK:-http://localhost:8080}"
REALM="${REALM:-cashflow}"
CLIENT_ID="${CLIENT_ID:-cashflow-api}"
CLIENT_SECRET="${CLIENT_SECRET:-cashflow-api-secret}"
MERCHANT_ID="${MERCHANT_ID:-0193e7a8-d8f0-7c5e-9b21-3f9f8a4d1c00}"
TARGET_DATE="${TARGET_DATE:-2026-05-13}"
COMPOSE_FILE="${COMPOSE_FILE:-infra/docker-compose.yml}"
COUNT="${COUNT:-100}"
CATCHUP_TIMEOUT="${CATCHUP_TIMEOUT:-90}"

for bin in curl jq docker; do
  command -v "$bin" >/dev/null 2>&1 || { echo >&2 "missing dep: $bin"; exit 127; }
done

# uuidgen é POSIX padrão em Linux/macOS; Git Bash do Windows não traz. Fallback:
# usa /proc/sys/kernel/random/uuid (Linux), powershell ([guid]::NewGuid) ou
# composição manual via $RANDOM como último recurso.
gen_uuid() {
  if command -v uuidgen >/dev/null 2>&1; then
    uuidgen
  elif [[ -r /proc/sys/kernel/random/uuid ]]; then
    cat /proc/sys/kernel/random/uuid
  elif command -v powershell.exe >/dev/null 2>&1; then
    powershell.exe -NoProfile -Command "[guid]::NewGuid().ToString()" | tr -d '\r'
  else
    printf '%08x-%04x-%04x-%04x-%012x' \
      $((RANDOM*RANDOM)) $RANDOM $RANDOM $RANDOM $((RANDOM*RANDOM*RANDOM))
  fi
}

# Por padrão usamos password grant (merchant1) porque o SA do client cashflow-api
# não tem `merchantId` setado no realm seed. Para usar client_credentials puro
# exporte GRANT_TYPE=client_credentials (e ajuste o realm).
#
# Atenção: usamos KC_USERNAME (não USERNAME) porque Git Bash no Windows já
# herda USERNAME do shell (=usuário Windows) e isso quebra silenciosamente o
# password grant. Igualmente para KC_PASSWORD.
GRANT_TYPE="${GRANT_TYPE:-password}"
KC_USERNAME="${KC_USERNAME:-merchant1@cashflow.local}"
KC_PASSWORD="${KC_PASSWORD:-merchant123}"

get_token() {
  if [[ "$GRANT_TYPE" == "password" ]]; then
    curl -fsS -X POST \
      "${KEYCLOAK}/realms/${REALM}/protocol/openid-connect/token" \
      -H 'Content-Type: application/x-www-form-urlencoded' \
      --data-urlencode "grant_type=password" \
      --data-urlencode "client_id=${CLIENT_ID}" \
      --data-urlencode "client_secret=${CLIENT_SECRET}" \
      --data-urlencode "username=${KC_USERNAME}" \
      --data-urlencode "password=${KC_PASSWORD}" \
      --data-urlencode "scope=openid" | jq -r '.access_token'
  else
    curl -fsS -X POST \
      "${KEYCLOAK}/realms/${REALM}/protocol/openid-connect/token" \
      -H 'Content-Type: application/x-www-form-urlencoded' \
      --data-urlencode "grant_type=client_credentials" \
      --data-urlencode "client_id=${CLIENT_ID}" \
      --data-urlencode "client_secret=${CLIENT_SECRET}" | jq -r '.access_token'
  fi
}

echo ">>> Token Keycloak"
TOKEN="$(get_token)"
[[ -n "$TOKEN" && "$TOKEN" != "null" ]] || { echo >&2 "Falha ao obter token"; exit 1; }

echo ">>> Derrubando Consolidation (api + worker + mongo)"
docker compose -f "$COMPOSE_FILE" stop consolidation-api consolidation-worker mongo

echo ">>> Disparando ${COUNT} lançamentos no Ledger (alvo: 100% 201)"
SUCCESS=0
FAIL=0
declare -a FAIL_STATUSES=()
for ((i=1; i<=COUNT; i++)); do
  IDEMP="$(gen_uuid)"
  STATUS=$(curl -s -o /dev/null -w '%{http_code}' \
    -X POST "${GATEWAY}/ledger/api/v1/entries" \
    -H "Authorization: Bearer ${TOKEN}" \
    -H "Idempotency-Key: ${IDEMP}" \
    -H "Content-Type: application/json" \
    -d "{\"type\":\"Credit\",\"amount\":{\"value\":10.00,\"currency\":\"BRL\"},\"description\":\"chaos ${i}\",\"category\":\"Sales\",\"entryDate\":\"${TARGET_DATE}\"}" \
    || echo "000")
  if [[ "$STATUS" == "201" ]]; then
    SUCCESS=$((SUCCESS+1))
  else
    FAIL=$((FAIL+1))
    FAIL_STATUSES+=("$STATUS")
  fi
done

echo "    -> 201: ${SUCCESS} / não-201: ${FAIL}"
if [[ ${FAIL} -ne 0 ]]; then
  echo >&2 "FALHA: Ledger foi afetado pela queda da Consolidação (não-201: ${FAIL_STATUSES[*]})"
  echo >&2 ">>> Restaurando serviços antes de sair"
  docker compose -f "$COMPOSE_FILE" start mongo consolidation-worker consolidation-api
  exit 1
fi

echo ">>> Restaurando Consolidation"
docker compose -f "$COMPOSE_FILE" start mongo consolidation-worker consolidation-api

echo ">>> Aguardando catch-up (timeout ${CATCHUP_TIMEOUT}s) — entriesCount >= ${COUNT}"
START=$(date +%s)
LAST_COUNT=0
while true; do
  TOKEN="$(get_token)"  # token expira em 5min; renova para evitar 401 no fim
  # 502/503 enquanto consolidation-api ainda está bootando — tolera e segue
  # contando como entriesCount=0 (próximo loop tenta de novo). Sem -f para não
  # propagar exit code; redireciona stderr.
  RESP=$(curl -sS "${GATEWAY}/consolidation/api/v1/balances/${MERCHANT_ID}/daily?date=${TARGET_DATE}" \
    -H "Authorization: Bearer ${TOKEN}" 2>/dev/null || echo '{}')
  ENTRIES=$(echo "$RESP" | jq -r '.entriesCount // 0' 2>/dev/null || echo 0)
  if [[ "$ENTRIES" =~ ^[0-9]+$ ]] && (( ENTRIES >= COUNT )); then
    LAST_COUNT="$ENTRIES"
    break
  fi
  LAST_COUNT="$ENTRIES"
  ELAPSED=$(( $(date +%s) - START ))
  if (( ELAPSED > CATCHUP_TIMEOUT )); then
    echo >&2 "FALHA: catch-up > ${CATCHUP_TIMEOUT}s (entriesCount=${LAST_COUNT})"
    exit 1
  fi
  sleep 2
done

ELAPSED=$(( $(date +%s) - START ))
echo "OK: NFR de isolamento validado."
echo "    - ${SUCCESS}/${COUNT} entries criadas no Ledger com Consolidation offline."
echo "    - Catch-up: ${ELAPSED}s (entriesCount=${LAST_COUNT})."
