import { expect, test, type Page } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";

const BASE_URL = process.env.BASE_URL ?? "http://localhost:3001";
const KEYCLOAK = process.env.KEYCLOAK_URL ?? "http://localhost:8080";
const GATEWAY = process.env.GATEWAY_URL ?? "http://localhost:8000";

const MERCHANT_USERNAME = "merchant1@cashflow.local";
const MERCHANT_PASSWORD = "merchant123";

const SHOTS_DIR = "screenshots";

async function ensureKeycloakReady() {
  for (let attempt = 0; attempt < 30; attempt++) {
    try {
      const response = await fetch(`${KEYCLOAK}/realms/cashflow/.well-known/openid-configuration`);
      if (response.ok) return;
    } catch {
      /* ignore */
    }
    await new Promise((resolve) => setTimeout(resolve, 2_000));
  }
  throw new Error("Keycloak did not become reachable");
}

async function loginViaKeycloak(page: Page) {
  await page.goto("/", { waitUntil: "domcontentloaded" });
  await expect(page).toHaveURL(/\/login/);
  await page.screenshot({ path: `${SHOTS_DIR}/01-login-page.png`, fullPage: true });

  await page.getByRole("button", { name: /Entrar com Keycloak/i }).click();

  await page.waitForURL(/keycloak|protocol\/openid-connect/, { timeout: 30_000 });
  await page.screenshot({ path: `${SHOTS_DIR}/02-keycloak-login.png`, fullPage: true });

  await page.locator("#username").fill(MERCHANT_USERNAME);
  await page.locator("#password").fill(MERCHANT_PASSWORD);
  await page.locator("#kc-login").click();

  await page.waitForURL(/\/entries(\b|\?|$)/, { timeout: 30_000 });
  await expect(page.getByRole("heading", { name: "Lançamentos", exact: true })).toBeVisible();
}

test.describe.configure({ mode: "serial" });

test.beforeAll(async () => {
  await ensureKeycloakReady();
});

test("FS-01 → FS-02: login Keycloak e listagem de lançamentos", async ({ page }) => {
  await loginViaKeycloak(page);
  await page.screenshot({ path: `${SHOTS_DIR}/03-entries-list.png`, fullPage: true });

  await expect(page.getByRole("button", { name: /Atualizar/i })).toBeVisible();

  // O contador é a evidência primária de que a listagem carregou.
  const counter = page.getByText(/^\d+ lançamento\(s\)$/);
  await expect(counter).toBeVisible();

  // Caso haja erro real, ele aparece com o texto "Tentar novamente".
  await expect(page.getByRole("button", { name: /Tentar novamente/i })).toHaveCount(0);

  // E ao menos uma linha da tabela deve estar visível (há seed).
  await expect(page.getByRole("row")).not.toHaveCount(0);
});

test("FS-03: criação de novo lançamento aparece imediatamente no Ledger", async ({ page }) => {
  await loginViaKeycloak(page);

  await page.getByRole("link", { name: /Novo lançamento/i }).first().click();
  await page.waitForURL(/\/entries\/new$/);
  await expect(page.getByRole("heading", { name: "Novo lançamento", exact: true })).toBeVisible();
  await page.screenshot({ path: `${SHOTS_DIR}/04-new-entry-form.png`, fullPage: true });

  const description = `E2E test ${Date.now()}`;
  await page.locator("form select").first().selectOption("Credit");
  await page.getByLabel("Valor *").fill("123,45");
  await page.getByLabel("Descrição *").fill(description);
  await page.getByLabel("Categoria").fill("E2E");
  await page.getByRole("button", { name: /Registrar lançamento/i }).click();

  await page.waitForURL(/\/entries$/);
  await expect(page.getByText(description)).toBeVisible({ timeout: 15_000 });
  await page.screenshot({ path: `${SHOTS_DIR}/05-entry-created.png`, fullPage: true });
});

test("FS-04: consolidado diário com 3 cards + gráfico pie", async ({ page }) => {
  await loginViaKeycloak(page);
  await page.goto("/balances/daily");

  await expect(page.getByRole("heading", { name: "Consolidado diário" })).toBeVisible();
  await expect(page.getByText(/Créditos do dia/i)).toBeVisible();
  await expect(page.getByText(/Débitos do dia/i)).toBeVisible();
  await expect(page.getByText(/Saldo do dia/i)).toBeVisible();
  await page.waitForTimeout(2500);
  await page.screenshot({ path: `${SHOTS_DIR}/06-daily-balance.png`, fullPage: true });

  await expect(
    page.locator('div[role="img"][aria-label*="categoria" i], div[role="img"][aria-label*="Distribu" i]'),
  ).toBeVisible({ timeout: 10_000 });
});

test("FS-05: scan de acessibilidade (axe) sem violações sérias/críticas", async ({ page }) => {
  await loginViaKeycloak(page);

  const violations: { url: string; issues: { id: string; impact: string | null }[] }[] = [];

  for (const url of ["/entries", "/entries/new", "/balances/daily"]) {
    await page.goto(url);
    if (url === "/balances/daily") await page.waitForTimeout(1500);
    const results = await new AxeBuilder({ page })
      .withTags(["wcag2a", "wcag2aa"])
      .analyze();
    const serious = results.violations.filter((v) => v.impact === "serious" || v.impact === "critical");
    if (serious.length > 0) {
      violations.push({
        url,
        issues: serious.map((v) => ({
          id: v.id,
          impact: v.impact ?? null,
          help: v.help,
          nodes: v.nodes.slice(0, 3).map((n) => ({ html: n.html, target: n.target })),
        })),
      });
    }
  }

  if (violations.length > 0) {
    console.log("AXE violations:", JSON.stringify(violations, null, 2));
  }
  expect(violations, JSON.stringify(violations, null, 2)).toEqual([]);
});

test("Swagger Ledger e Consolidation acessíveis (gateway expõe APIs por trás de JWT)", async ({ page }) => {
  // O Gateway YARP propaga somente rotas /api/v1/* das APIs e exige JWT
  // (401 em /ledger/swagger, /consolidation/swagger). A documentação OpenAPI
  // é exposta diretamente pelas APIs em :8001 (Ledger) e :8002 (Consolidation).
  const targets = [
    { name: "ledger-swagger", url: "http://localhost:8001/swagger/index.html" },
    { name: "ledger-scalar", url: "http://localhost:8001/scalar/v1" },
    { name: "consolidation-swagger", url: "http://localhost:8002/swagger/index.html" },
    { name: "consolidation-scalar", url: "http://localhost:8002/scalar/v1" },
  ];

  for (const target of targets) {
    const response = await page.goto(target.url);
    expect(response, `Falha ao abrir ${target.url}`).not.toBeNull();
    expect(response!.status(), target.url).toBeLessThan(400);
    await page.waitForLoadState("networkidle").catch(() => undefined);
    await page.screenshot({ path: `${SHOTS_DIR}/07-${target.name}.png`, fullPage: true });
  }
});
