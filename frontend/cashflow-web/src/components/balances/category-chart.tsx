"use client";

import { Cell, Legend, Pie, PieChart, ResponsiveContainer, Tooltip } from "recharts";

import type { DailyBalanceCategory } from "@/lib/types";
import { formatCurrencyBrl } from "@/lib/utils";

const PALETTE = [
  "hsl(222, 76%, 48%)",
  "hsl(158, 64%, 42%)",
  "hsl(32, 95%, 50%)",
  "hsl(274, 72%, 56%)",
  "hsl(348, 80%, 52%)",
  "hsl(192, 72%, 42%)",
];

type CategoryChartProps = {
  categories: DailyBalanceCategory[];
};

export function CategoryChart({ categories }: CategoryChartProps) {
  const data = categories
    .map((category) => ({
      name: category.category || "Sem categoria",
      value: Number((category.credit + category.debit).toFixed(2)),
      credit: category.credit,
      debit: category.debit,
      count: category.count,
    }))
    .filter((entry) => entry.value > 0);

  if (data.length === 0) {
    return (
      <p className="py-10 text-center text-sm text-muted-foreground">
        Nenhum valor movimentado por categoria.
      </p>
    );
  }

  return (
    <div className="h-72 w-full" role="img" aria-label="Distribuição de movimentações por categoria">
      <ResponsiveContainer width="100%" height="100%">
        <PieChart>
          <Pie
            data={data}
            dataKey="value"
            nameKey="name"
            cx="50%"
            cy="50%"
            outerRadius="80%"
            isAnimationActive={false}
            label={({ name, percent }) => `${name} ${(percent * 100).toFixed(0)}%`}
          >
            {data.map((entry, index) => (
              <Cell key={entry.name} fill={PALETTE[index % PALETTE.length]} />
            ))}
          </Pie>
          <Tooltip
            formatter={(value: number) => formatCurrencyBrl(value)}
            labelFormatter={(label) => `Categoria: ${label}`}
          />
          <Legend />
        </PieChart>
      </ResponsiveContainer>
    </div>
  );
}
