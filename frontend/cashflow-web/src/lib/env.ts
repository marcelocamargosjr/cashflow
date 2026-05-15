function required(name: string, value: string | undefined, fallback?: string): string {
  if (value && value.length > 0) return value;
  if (fallback !== undefined) return fallback;
  throw new Error(`Missing required environment variable: ${name}`);
}

export const serverEnv = {
  keycloakIssuer: required(
    "KEYCLOAK_ISSUER",
    process.env.KEYCLOAK_ISSUER,
    "http://localhost:8080/realms/cashflow",
  ),
  keycloakClientId: required(
    "KEYCLOAK_CLIENT_ID",
    process.env.KEYCLOAK_CLIENT_ID,
    "cashflow-web",
  ),
  keycloakClientSecret: process.env.KEYCLOAK_CLIENT_SECRET ?? "",
  nextAuthSecret: required(
    "NEXTAUTH_SECRET",
    process.env.NEXTAUTH_SECRET,
    "dev-only-secret-change-me-please-32-chars-min",
  ),
  nextAuthUrl: required(
    "NEXTAUTH_URL",
    process.env.NEXTAUTH_URL,
    "http://localhost:3001",
  ),
  apiBaseUrl: required(
    "CASHFLOW_API_BASE_URL",
    process.env.CASHFLOW_API_BASE_URL,
    "http://localhost:8000",
  ),
};

export const publicEnv = {
  apiBaseUrl:
    process.env.NEXT_PUBLIC_CASHFLOW_API_BASE_URL ?? "http://localhost:8000",
};
