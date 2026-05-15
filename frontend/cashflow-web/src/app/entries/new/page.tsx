import type { Metadata } from "next";

import { NewEntryForm } from "@/components/entries/new-entry-form";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { requireSession } from "@/lib/session";

export const metadata: Metadata = {
  title: "Novo lançamento — Cashflow",
};

export const dynamic = "force-dynamic";

export default async function NewEntryPage() {
  await requireSession("/entries/new");

  return (
    <section aria-labelledby="new-entry-title" className="mx-auto max-w-2xl">
      <header className="mb-6">
        <h1 id="new-entry-title" className="text-2xl font-semibold tracking-tight">
          Novo lançamento
        </h1>
        <p className="text-sm text-muted-foreground">
          Registre um crédito ou débito. Após confirmação aparece imediatamente no Ledger
          e em poucos segundos no consolidado diário.
        </p>
      </header>
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Informações do lançamento</CardTitle>
          <CardDescription>
            Campos obrigatórios marcados com asterisco (*).
          </CardDescription>
        </CardHeader>
        <CardContent>
          <NewEntryForm />
        </CardContent>
      </Card>
    </section>
  );
}
