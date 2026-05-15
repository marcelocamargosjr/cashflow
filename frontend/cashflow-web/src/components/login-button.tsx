"use client";

import * as React from "react";
import { signIn } from "next-auth/react";
import { Loader2 } from "lucide-react";

import { Button } from "@/components/ui/button";

type LoginButtonProps = {
  callbackUrl?: string;
};

export function LoginButton({ callbackUrl = "/entries" }: LoginButtonProps) {
  const [loading, setLoading] = React.useState(false);

  return (
    <Button
      className="w-full"
      size="lg"
      disabled={loading}
      aria-busy={loading}
      onClick={() => {
        setLoading(true);
        signIn("keycloak", { callbackUrl }).catch(() => setLoading(false));
      }}
    >
      {loading ? (
        <>
          <Loader2 className="mr-2 h-4 w-4 animate-spin" aria-hidden="true" />
          Redirecionando…
        </>
      ) : (
        "Entrar com Keycloak"
      )}
    </Button>
  );
}
