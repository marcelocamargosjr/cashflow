"use client";

import { keepPreviousData, useQuery } from "@tanstack/react-query";

import { apiFetch } from "@/lib/api-client";
import type { EntriesFilters, EntriesPage } from "@/lib/types";

function buildQueryString(filters: EntriesFilters) {
  const params = new URLSearchParams();
  params.set("from", filters.from);
  params.set("to", filters.to);
  if (filters.type) params.set("type", filters.type);
  if (filters.category) params.set("category", filters.category);
  params.set("page", String(filters.page));
  params.set("size", String(filters.size));
  return params.toString();
}

export function useEntries(filters: EntriesFilters) {
  return useQuery<EntriesPage>({
    queryKey: ["entries", filters],
    queryFn: ({ signal }) =>
      apiFetch<EntriesPage>(`/ledger/api/v1/entries?${buildQueryString(filters)}`, { signal }),
    placeholderData: keepPreviousData,
  });
}
