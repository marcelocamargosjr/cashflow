"use client";

import * as React from "react";
import Link from "next/link";
import {
  flexRender,
  getCoreRowModel,
  useReactTable,
  type ColumnDef,
} from "@tanstack/react-table";
import { AlertCircle, Plus, RefreshCw } from "lucide-react";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { useToast } from "@/components/ui/use-toast";
import { useEntries } from "@/hooks/use-entries";
import { ApiError } from "@/lib/api-client";
import type { EntriesFilters, Entry } from "@/lib/types";
import { formatCurrencyBrl, formatDateBr } from "@/lib/utils";

const ENTRY_TYPE_LABELS: Record<Entry["type"], string> = {
  Credit: "Crédito",
  Debit: "Débito",
};
const ENTRY_STATUS_LABELS: Record<Entry["status"], string> = {
  Confirmed: "Confirmado",
  Reversed: "Estornado",
};

const columns: ColumnDef<Entry>[] = [
  {
    accessorKey: "entryDate",
    header: "Data",
    cell: ({ getValue }) => formatDateBr(getValue<string>()),
  },
  {
    accessorKey: "type",
    header: "Tipo",
    cell: ({ getValue }) => ENTRY_TYPE_LABELS[getValue<Entry["type"]>()],
  },
  {
    accessorKey: "description",
    header: "Descrição",
    cell: ({ getValue }) => (
      <span className="block max-w-[28ch] truncate" title={getValue<string>()}>
        {getValue<string>()}
      </span>
    ),
  },
  {
    accessorKey: "category",
    header: "Categoria",
    cell: ({ getValue }) => getValue<string | null>() ?? "—",
  },
  {
    id: "amount",
    header: () => <span className="block text-right">Valor</span>,
    cell: ({ row }) => {
      const sign = row.original.type === "Debit" ? -1 : 1;
      const formatted = formatCurrencyBrl(sign * row.original.amount.value);
      return (
        <span
          className={`block text-right font-mono tabular-nums ${
            row.original.type === "Debit" ? "text-destructive" : "text-emerald-700"
          }`}
        >
          {formatted}
        </span>
      );
    },
  },
  {
    accessorKey: "status",
    header: "Status",
    cell: ({ getValue }) => ENTRY_STATUS_LABELS[getValue<Entry["status"]>()],
  },
];

type EntriesViewProps = {
  defaultFilters: EntriesFilters;
};

export function EntriesView({ defaultFilters }: EntriesViewProps) {
  const [filters, setFilters] = React.useState<EntriesFilters>(defaultFilters);
  const [pendingFrom, setPendingFrom] = React.useState(defaultFilters.from);
  const [pendingTo, setPendingTo] = React.useState(defaultFilters.to);
  const [pendingType, setPendingType] = React.useState<EntriesFilters["type"]>(defaultFilters.type);
  const [pendingCategory, setPendingCategory] = React.useState(defaultFilters.category ?? "");
  const { toast } = useToast();

  const query = useEntries(filters);
  const data = query.data?.items ?? [];

  const table = useReactTable({
    data,
    columns,
    getCoreRowModel: getCoreRowModel(),
  });

  const errorMessage =
    query.error instanceof ApiError
      ? query.error.message
      : query.error
        ? "Não foi possível carregar os lançamentos."
        : null;

  function applyFilters(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFilters({
      from: pendingFrom,
      to: pendingTo,
      type: pendingType ?? "",
      category: pendingCategory,
      page: 1,
      size: defaultFilters.size,
    });
  }

  function retry() {
    toast({ title: "Recarregando lançamentos…" });
    query.refetch();
  }

  return (
    <section aria-labelledby="entries-title" className="space-y-6">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 id="entries-title" className="text-2xl font-semibold tracking-tight">
            Lançamentos
          </h1>
          <p className="text-sm text-muted-foreground">
            Consulte e filtre os lançamentos confirmados no Ledger.
          </p>
        </div>
        <Button asChild>
          <Link href="/entries/new" aria-label="Adicionar novo lançamento">
            <Plus className="mr-2 h-4 w-4" aria-hidden="true" /> Novo lançamento
          </Link>
        </Button>
      </header>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Filtros</CardTitle>
        </CardHeader>
        <CardContent>
          <form
            onSubmit={applyFilters}
            className="grid gap-4 md:grid-cols-[repeat(4,minmax(0,1fr))_auto]"
            aria-label="Filtros de lançamentos"
          >
            <div className="space-y-1">
              <Label htmlFor="filter-from">De</Label>
              <Input
                id="filter-from"
                type="date"
                value={pendingFrom}
                max={pendingTo}
                onChange={(event) => setPendingFrom(event.target.value)}
                required
              />
            </div>
            <div className="space-y-1">
              <Label htmlFor="filter-to">Até</Label>
              <Input
                id="filter-to"
                type="date"
                value={pendingTo}
                min={pendingFrom}
                onChange={(event) => setPendingTo(event.target.value)}
                required
              />
            </div>
            <div className="space-y-1">
              <Label htmlFor="filter-type">Tipo</Label>
              <select
                id="filter-type"
                className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
                value={pendingType ?? ""}
                onChange={(event) =>
                  setPendingType(event.target.value as EntriesFilters["type"])
                }
              >
                <option value="">Todos</option>
                <option value="Credit">Crédito</option>
                <option value="Debit">Débito</option>
              </select>
            </div>
            <div className="space-y-1">
              <Label htmlFor="filter-category">Categoria</Label>
              <Input
                id="filter-category"
                type="text"
                placeholder="ex.: Sales"
                value={pendingCategory}
                onChange={(event) => setPendingCategory(event.target.value)}
                maxLength={50}
              />
            </div>
            <div className="flex items-end">
              <Button type="submit" className="w-full md:w-auto">
                Aplicar
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="flex flex-row items-center justify-between space-y-0">
          <CardTitle className="text-base">
            {query.data
              ? `${query.data.total} lançamento(s)`
              : "Carregando lançamentos…"}
          </CardTitle>
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={retry}
            disabled={query.isFetching}
            aria-label="Atualizar lançamentos"
          >
            <RefreshCw
              className={`mr-2 h-4 w-4 ${query.isFetching ? "animate-spin" : ""}`}
              aria-hidden="true"
            />
            Atualizar
          </Button>
        </CardHeader>
        <CardContent>
          {errorMessage ? (
            <div
              role="alert"
              className="flex items-center justify-between gap-3 rounded-md border border-destructive/40 bg-destructive/5 p-4 text-sm text-destructive"
            >
              <div className="flex items-center gap-2">
                <AlertCircle className="h-4 w-4" aria-hidden="true" />
                <span>{errorMessage}</span>
              </div>
              <Button variant="outline" size="sm" onClick={retry}>
                Tentar novamente
              </Button>
            </div>
          ) : query.isPending ? (
            <EntriesSkeleton rows={filters.size > 8 ? 8 : filters.size} />
          ) : data.length === 0 ? (
            <div className="rounded-md border border-dashed p-10 text-center text-sm text-muted-foreground">
              Nenhum lançamento encontrado para os filtros selecionados.
            </div>
          ) : (
            <Table aria-describedby="entries-title">
              <TableHeader>
                {table.getHeaderGroups().map((headerGroup) => (
                  <TableRow key={headerGroup.id}>
                    {headerGroup.headers.map((header) => (
                      <TableHead key={header.id} scope="col">
                        {header.isPlaceholder
                          ? null
                          : flexRender(header.column.columnDef.header, header.getContext())}
                      </TableHead>
                    ))}
                  </TableRow>
                ))}
              </TableHeader>
              <TableBody>
                {table.getRowModel().rows.map((row) => (
                  <TableRow key={row.id}>
                    {row.getVisibleCells().map((cell) => (
                      <TableCell key={cell.id}>
                        {flexRender(cell.column.columnDef.cell, cell.getContext())}
                      </TableCell>
                    ))}
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </section>
  );
}

function EntriesSkeleton({ rows }: { rows: number }) {
  return (
    <div className="space-y-3" aria-label="Carregando lançamentos">
      {Array.from({ length: rows }).map((_, index) => (
        <div key={index} className="flex items-center gap-4">
          <Skeleton className="h-4 w-24" />
          <Skeleton className="h-4 w-16" />
          <Skeleton className="h-4 flex-1" />
          <Skeleton className="h-4 w-24" />
          <Skeleton className="h-4 w-24" />
          <Skeleton className="h-4 w-20" />
        </div>
      ))}
    </div>
  );
}
