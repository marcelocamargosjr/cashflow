"use client";

import * as React from "react";
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
  const data = React.useMemo(
    () =>
      categories
        .map((category) => ({
          name: category.category || "Sem categoria",
          value: Number((category.credit + category.debit).toFixed(2)),
          credit: category.credit,
          debit: category.debit,
          count: category.count,
        }))
        .filter((entry) => entry.value > 0),
    [categories],
  );

  // Recharts não permite controlar role/aria nos SVGs internos. Para satisfazer
  // a regra axe `svg-img-alt`, marcamos os SVGs renderizados como decorativos
  // (`aria-hidden`) — o wrapper carrega `role="img"` + `aria-label` descritivo
  // e a tabela textual a seguir é a alternativa acessível. Como o
  // ResponsiveContainer monta os <svg> de forma assíncrona (após medir o
  // tamanho), usamos um MutationObserver para reaplicar os atributos a cada
  // (re)renderização.
  const containerRef = React.useRef<HTMLDivElement>(null);
  React.useEffect(() => {
    const node = containerRef.current;
    if (!node) return;
    const apply = () => {
      // Recharts marca tanto o <svg> raiz quanto cada <path> setor com
      // role="img" — todos disparam `svg-img-alt`. Marcamos todos como
      // apresentação (o wrapper já tem role=img + aria-label). Não usamos
      // aria-hidden por causa de `aria-hidden-focus` quando há elementos
      // focáveis (legenda) na árvore.
      node.querySelectorAll('svg, [role="img"]').forEach((el) => {
        if (el.getAttribute("role") !== "presentation") {
          el.setAttribute("role", "presentation");
        }
        if (el.tagName.toLowerCase() === "svg" && el.getAttribute("focusable") !== "false") {
          el.setAttribute("focusable", "false");
        }
        el.removeAttribute("aria-hidden");
      });
    };
    apply();
    // Observa apenas mudanças de filhos (novos <svg> sendo montados); evita
    // observar atributos para não cair em loop com o próprio setAttribute.
    const observer = new MutationObserver(apply);
    observer.observe(node, { childList: true, subtree: true });
    return () => observer.disconnect();
  }, [data]);

  if (data.length === 0) {
    return (
      <p className="py-10 text-center text-sm text-muted-foreground">
        Nenhum valor movimentado por categoria.
      </p>
    );
  }

  return (
    <div className="space-y-4">
      <div
        ref={containerRef}
        className="h-72 w-full"
        role="img"
        aria-label="Gráfico de pizza: distribuição de movimentações por categoria"
      >
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
      <table className="sr-only">
        <caption>Distribuição por categoria (versão textual do gráfico)</caption>
        <thead>
          <tr>
            <th scope="col">Categoria</th>
            <th scope="col">Total movimentado</th>
            <th scope="col">Lançamentos</th>
          </tr>
        </thead>
        <tbody>
          {data.map((entry) => (
            <tr key={entry.name}>
              <th scope="row">{entry.name}</th>
              <td>{formatCurrencyBrl(entry.value)}</td>
              <td>{entry.count}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
