/**
 * Cenário NFR literal — `08-TESTES.md §6.1`.
 *
 * Carga: 50 req/s sustentado por 60s = 3000 requests.
 * Threshold: `http_req_failed: rate<0.05` (≤ 5% perda — NFR literal do PDF).
 *
 * Pré-condições:
 *   - `make up && make seed`
 *   - env BASE_URL, TOKEN, MERCHANT_ID injetados via `scripts/make-perf.sh`
 *
 * Output JSON: salvo via `--summary-export=/scripts/k6-result.json` (mapeado
 * pelo make-perf para `docs/performance/k6-result-YYYY-MM-DD.json`).
 */
import http from 'k6/http';
import { check } from 'k6';

export const options = {
  scenarios: {
    sustained: {
      executor: 'constant-arrival-rate',
      rate: 50,
      timeUnit: '1s',
      duration: '60s',
      preAllocatedVUs: 50,
      maxVUs: 200,
    },
  },
  thresholds: {
    // Critério literal do PDF — falha o teste se ultrapassar.
    http_req_failed: ['rate<0.05'],
    // Latência agregada — cache TTL 60s domina após o primeiro hit.
    http_req_duration: ['p(95)<500'],
    checks: ['rate>0.95'],
  },
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)'],
};

const BASE = __ENV.BASE_URL || 'http://gateway:8080';
const TOKEN = __ENV.TOKEN || '';
const MERCHANT_ID = __ENV.MERCHANT_ID;
const TARGET_DATE = __ENV.TARGET_DATE || '2026-05-13';

if (!MERCHANT_ID) {
  throw new Error('MERCHANT_ID env var is required');
}

export default function () {
  const res = http.get(
    `${BASE}/consolidation/api/v1/balances/${MERCHANT_ID}/daily?date=${TARGET_DATE}`,
    {
      headers: {
        Authorization: `Bearer ${TOKEN}`,
        Accept: 'application/json',
      },
      tags: { name: 'GetDailyBalance' },
    }
  );

  check(res, {
    'status is 200': (r) => r.status === 200,
    'has totalCredits field': (r) => r.json('totalCredits') !== undefined || r.status !== 200,
  });
}
