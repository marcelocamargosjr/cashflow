"use client";

import * as React from "react";
import dynamic from "next/dynamic";
import { AlertCircle, RefreshCw } from "lucide-react";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { useDailyBalance } from "@/hooks/use-daily-balance";
import { ApiError } from "@/lib/api-client";
import type { DailyBalance } from "@/lib/types";
import { formatCurrencyBrl, formatDateTimeBr } from "@/lib/utils";

const CategoryChart = dynamic(
  () => import("@/components/balances/category-chart").then((mod) => mod.CategoryChart),
  { ssr: false, loading: () => <Skeleton className="h-72 w-full" /> },
);

type DailyBalanceViewProps = {
  merchantId: string | null;
  initialDate: string;
};

export function DailyBalanceView({ merchantId, initialDate }: DailyBalanceViewProps) {
  const [date, setDate] = React.useState(initialDate);
  const query = useDailyBalance(merchantId, date);

  if (!merchantId) {
    return (
      <div
        role="alert"
        className="rounded-md border border-destructive/40 bg-destructive/5 p-4 text-sm text-destructive"
      >
        Não foi possível identificar o comerciante na sessão. Faça login novamente.
      </div>
    );
  }

  const data = query.data;
  const errorMessage =
    query.error instanceof ApiError
      ? query.error.message
      : query.error
        ? "Não foi possível carregar o consolidado."
        : null;

  return (
    <section aria-labelledby="balance-title" className="space-y-6">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 id="balance-title" className="text-2xl font-semibold tracking-tight">
            Consolidado diário
          </h1>
          <p className="text-sm text-muted-foreground">
            Totais por dia projetados a partir dos lançamentos confirmados.
          </p>
        </div>
        <form
          className="flex items-end gap-3"
          aria-label="Selecionar data do consolidado"
          onSubmit={(event) => event.preventDefault()}
        >
          <div className="space-y-1">
            <Label htmlFor="balance-date">Data</Label>
            <Input
              id="balance-date"
              type="date"
              value={date}
              onChange={(event) => setDate(event.target.value)}
            />
          </div>
          <Button
            type="button"
            variant="outline"
            onClick={() => query.refetch()}
            aria-label="Atualizar consolidado"
            disabled={query.isFetching}
          >
            <RefreshCw
              className={`mr-2 h-4 w-4 ${query.isFetching ? "animate-spin" : ""}`}
              aria-hidden="true"
            />
            Atualizar
          </Button>
        </form>
      </header>

      {errorMessage ? (
        <div
          role="alert"
          className="flex items-center gap-2 rounded-md border border-destructive/40 bg-destructive/5 p-4 text-sm text-destructive"
        >
          <AlertCircle className="h-4 w-4" aria-hidden="true" />
          <span>{errorMessage}</span>
        </div>
      ) : null}

      <div className="grid gap-4 md:grid-cols-3">
        <SummaryCard
          label="Créditos do dia"
          value={data?.totalCredits}
          tone="positive"
          loading={query.isPending}
        />
        <SummaryCard
          label="Débitos do dia"
          value={data?.totalDebits}
          tone="negative"
          loading={query.isPending}
        />
        <SummaryCard
          label="Saldo do dia"
          value={data?.balance}
          tone="neutral"
          loading={query.isPending}
        />
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Distribuição por categoria</CardTitle>
        </CardHeader>
        <CardContent>
          <ChartArea data={data} loading={query.isPending} />
        </CardContent>
      </Card>

      <FreshnessIndicator data={data} loading={query.isPending} />
    </section>
  );
}

function SummaryCard({
  label,
  value,
  tone,
  loading,
}: {
  label: string;
  value: number | undefined;
  tone: "positive" | "negative" | "neutral";
  loading: boolean;
}) {
  const toneClass =
    tone === "positive"
      ? "text-emerald-700"
      : tone === "negative"
        ? "text-destructive"
        : "text-foreground";
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-sm font-medium text-muted-foreground">{label}</CardTitle>
      </CardHeader>
      <CardContent>
        {loading ? (
          <Skeleton className="h-9 w-32" />
        ) : (
          <p className={`font-mono text-3xl font-semibold tabular-nums ${toneClass}`}>
            {formatCurrencyBrl(value ?? 0)}
          </p>
        )}
      </CardContent>
    </Card>
  );
}

function ChartArea({
  data,
  loading,
}: {
  data: DailyBalance | undefined;
  loading: boolean;
}) {
  if (loading) return <Skeleton className="h-72 w-full" />;
  const categories = data?.byCategory ?? [];
  if (categories.length === 0) {
    return (
      <p className="py-10 text-center text-sm text-muted-foreground">
        Nenhuma categoria registrada para a data selecionada.
      </p>
    );
  }
  return <CategoryChart categories={categories} />;
}

function FreshnessIndicator({
  data,
  loading,
}: {
  data: DailyBalance | undefined;
  loading: boolean;
}) {
  if (loading) return null;
  if (!data) return null;
  const updatedAt = new Date(data.lastUpdatedAt);
  const secondsAgo = Math.max(0, Math.floor((Date.now() - updatedAt.getTime()) / 1000));
  return (
    <p
      aria-live="polite"
      className="text-xs text-muted-foreground"
      title={formatDateTimeBr(updatedAt)}
    >
      Atualizado há {secondsAgo}s · revisão #{data.revision}
      {data.cache?.hit ? ` · cache (${data.cache.ageSeconds}s)` : ""}
    </p>
  );
}
