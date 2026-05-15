import type { AuthOptions } from "next-auth";
import KeycloakProvider from "next-auth/providers/keycloak";

import { serverEnv } from "@/lib/env";

type DecodedAccessToken = {
  exp?: number;
  realm_access?: { roles?: string[] };
  merchantId?: string;
  preferred_username?: string;
};

function decodeJwtPayload(token: string): DecodedAccessToken {
  try {
    const [, payload] = token.split(".");
    if (!payload) return {};
    const normalized = payload.replace(/-/g, "+").replace(/_/g, "/");
    const json = Buffer.from(normalized, "base64").toString("utf8");
    return JSON.parse(json) as DecodedAccessToken;
  } catch {
    return {};
  }
}

async function refreshAccessToken(refreshToken: string) {
  const params = new URLSearchParams({
    grant_type: "refresh_token",
    client_id: serverEnv.keycloakClientId,
    refresh_token: refreshToken,
  });
  if (serverEnv.keycloakClientSecret) {
    params.set("client_secret", serverEnv.keycloakClientSecret);
  }

  const response = await fetch(`${serverEnv.keycloakIssuer}/protocol/openid-connect/token`, {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body: params,
    cache: "no-store",
  });

  if (!response.ok) {
    throw new Error(`Failed to refresh Keycloak token: ${response.status}`);
  }

  return (await response.json()) as {
    access_token: string;
    refresh_token?: string;
    expires_in: number;
  };
}

export const authOptions: AuthOptions = {
  secret: serverEnv.nextAuthSecret,
  session: { strategy: "jwt" },
  providers: [
    KeycloakProvider({
      clientId: serverEnv.keycloakClientId,
      clientSecret: serverEnv.keycloakClientSecret,
      issuer: serverEnv.keycloakIssuer,
      authorization: { params: { scope: "openid profile email" } },
      checks: ["pkce", "state"],
    }),
  ],
  callbacks: {
    async jwt({ token, account }) {
      if (account?.access_token) {
        const decoded = decodeJwtPayload(account.access_token);
        token.accessToken = account.access_token;
        token.refreshToken = account.refresh_token;
        token.expiresAt = account.expires_at
          ? account.expires_at * 1000
          : decoded.exp
            ? decoded.exp * 1000
            : Date.now() + 4 * 60 * 1000;
        token.merchantId = decoded.merchantId ?? null;
        token.roles = decoded.realm_access?.roles ?? [];
        token.username = decoded.preferred_username ?? null;
        return token;
      }

      if (token.expiresAt && Date.now() < (token.expiresAt as number) - 30_000) {
        return token;
      }

      if (token.refreshToken) {
        try {
          const refreshed = await refreshAccessToken(token.refreshToken as string);
          const decoded = decodeJwtPayload(refreshed.access_token);
          token.accessToken = refreshed.access_token;
          if (refreshed.refresh_token) token.refreshToken = refreshed.refresh_token;
          token.expiresAt = Date.now() + refreshed.expires_in * 1000;
          token.merchantId = decoded.merchantId ?? token.merchantId ?? null;
          token.roles = decoded.realm_access?.roles ?? token.roles ?? [];
          token.error = undefined;
        } catch {
          token.error = "RefreshAccessTokenError";
        }
      }

      return token;
    },
    async session({ session, token }) {
      session.accessToken = token.accessToken as string | undefined;
      session.merchantId = (token.merchantId as string | null) ?? null;
      session.roles = (token.roles as string[] | undefined) ?? [];
      session.username = (token.username as string | null) ?? null;
      session.error = token.error as string | undefined;
      return session;
    },
  },
  pages: {
    signIn: "/login",
    error: "/login",
  },
};
