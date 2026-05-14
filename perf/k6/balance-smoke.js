/**
 * Smoke test — `08-TESTES.md §6.2`. 10 req/s × 10s para CI.
 * Threshold relaxado: a ideia é só validar que o caminho está vivo, não fazer NFR.
 */
import http from 'k6/http';
import { check } from 'k6';

export const options = {
  scenarios: {
    smoke: {
      executor: 'constant-arrival-rate',
      rate: 10,
      timeUnit: '1s',
      duration: '10s',
      preAllocatedVUs: 10,
      maxVUs: 50,
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.10'],
    http_req_duration: ['p(95)<1500'],
    checks: ['rate>0.90'],
  },
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
      headers: { Authorization: `Bearer ${TOKEN}` },
      tags: { name: 'GetDailyBalance.Smoke' },
    }
  );
  check(res, { 'status 200': (r) => r.status === 200 });
}
