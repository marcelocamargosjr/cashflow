// Auditoria Lighthouse focada em Accessibility. Para as páginas autenticadas
// usamos Playwright para fazer login OIDC e copiamos os cookies de sessão
// para uma instância Chrome controlada pelo Lighthouse (compartilhando o
// mesmo perfil via chrome-launcher userDataDir).
import { chromium } from "playwright";
import * as chromeLauncher from "chrome-launcher";
import lighthouse from "lighthouse";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";
import { mkdirSync, rmSync } from "node:fs";

const BASE_URL = process.env.BASE_URL ?? "http://localhost:3001";
const USERNAME = process.env.E2E_USERNAME ?? "merchant1@cashflow.local";
const PASSWORD = process.env.E2E_PASSWORD ?? "merchant123";
const MIN_SCORE = Number(process.env.MIN_A11Y_SCORE ?? "95");

const here = dirname(fileURLToPath(import.meta.url));
const profileDir = join(here, "..", ".tmp", "lighthouse-profile");
rmSync(profileDir, { recursive: true, force: true });
mkdirSync(profileDir, { recursive: true });

// 1) Login com Playwright apontando para o mesmo perfil que o Lighthouse usará.
const context = await chromium.launchPersistentContext(profileDir, {
  headless: true,
});
const page = await context.newPage();
await page.goto(`${BASE_URL}/login`);
await page.getByRole("button", { name: /Entrar com Keycloak/i }).click();
await page.waitForURL(/keycloak|protocol\/openid-connect/, { timeout: 30_000 });
await page.locator("#username").fill(USERNAME);
await page.locator("#password").fill(PASSWORD);
await page.locator("#kc-login").click();
await page.waitForURL(/\/entries/, { timeout: 30_000 });
await context.close();

// 2) Sobe Chrome novamente sobre o mesmo perfil — o cookie de sessão do
// NextAuth (next-auth.session-token) já está persistido em disco.
const chrome = await chromeLauncher.launch({
  chromeFlags: [
    "--headless=new",
    "--no-sandbox",
    "--disable-gpu",
    `--user-data-dir=${profileDir}`,
  ],
});

const urls = process.env.LH_URLS?.split(",") ?? [
  "/login",
  "/entries",
  "/entries/new",
  "/balances/daily",
];

const results = [];
let failed = false;

try {
  for (const path of urls) {
    const url = `${BASE_URL}${path}`;
    const runner = await lighthouse(url, {
      port: chrome.port,
      output: "json",
      logLevel: "error",
      onlyCategories: ["accessibility"],
    });
    const score = Math.round(runner.lhr.categories.accessibility.score * 100);
    const audits = Object.values(runner.lhr.audits)
      .filter((a) => a.score !== null && a.score < 1 && a.scoreDisplayMode !== "notApplicable")
      .map((a) => ({ id: a.id, title: a.title, score: a.score }));
    results.push({ url, score, failedAudits: audits });
    if (score < MIN_SCORE) failed = true;
    console.log(`${url} → Accessibility ${score}`);
    for (const a of audits) console.log(`   - ${a.id}: ${a.title}`);
  }
} finally {
  await chrome.kill();
}

console.log("---");
console.log(JSON.stringify(results, null, 2));

if (failed) {
  console.error(`Lighthouse Accessibility score abaixo de ${MIN_SCORE} em alguma página.`);
  process.exit(1);
}
