import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { getServerSession } from "next-auth";

import { LoginButton } from "@/components/login-button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { authOptions } from "@/lib/auth";

export const metadata: Metadata = {
  title: "Entrar — Cashflow",
};

type LoginSearchParams = {
  callbackUrl?: string | string[];
  error?: string | string[];
};

function first(value: string | string[] | undefined): string | undefined {
  if (Array.isArray(value)) return value[0];
  return value;
}

const errorMessages: Record<string, string> = {
  Configuration: "Falha na configuração do provedor de identidade. Tente novamente.",
  AccessDenied: "Acesso negado. Verifique se o usuário possui a role merchant.",
  Verification: "O link de verificação expirou. Inicie o login novamente.",
  OAuthSignin: "Não foi possível iniciar o fluxo OAuth com o Keycloak.",
  OAuthCallback: "Erro no callback OIDC. Tente novamente.",
  RefreshAccessTokenError: "Sua sessão expirou. Entre novamente.",
};

export default async function LoginPage({
  searchParams,
}: {
  searchParams?: LoginSearchParams;
}) {
  const session = await getServerSession(authOptions);
  const callbackUrl = first(searchParams?.callbackUrl) ?? "/entries";
  if (session?.accessToken && !session.error) {
    redirect(callbackUrl);
  }

  const errorCode = first(searchParams?.error);
  const errorMessage = errorCode ? errorMessages[errorCode] ?? errorMessages.OAuthCallback : null;

  return (
    <div className="mx-auto max-w-md py-12">
      <Card>
        <CardHeader>
          <CardTitle>Acessar o Cashflow</CardTitle>
          <CardDescription>
            Entre com sua conta de comerciante autenticada pelo Keycloak.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {errorMessage ? (
            <div
              role="alert"
              className="rounded-md border border-destructive/40 bg-destructive/5 p-3 text-sm text-destructive"
            >
              {errorMessage}
            </div>
          ) : null}
          <LoginButton callbackUrl={callbackUrl} />
          <p className="text-xs text-muted-foreground">
            Você será redirecionado para o servidor de identidade em <code>localhost:8080</code>.
          </p>
        </CardContent>
      </Card>
    </div>
  );
}
