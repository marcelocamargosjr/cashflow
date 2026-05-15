"use client";

import { useQuery } from "@tanstack/react-query";

import { apiFetch } from "@/lib/api-client";
import type { DailyBalance } from "@/lib/types";

export function useDailyBalance(merchantId: string | null | undefined, date: string) {
  return useQuery<DailyBalance>({
    enabled: Boolean(merchantId && date),
    queryKey: ["daily-balance", merchantId, date],
    queryFn: ({ signal }) =>
      apiFetch<DailyBalance>(
        `/consolidation/api/v1/balances/${merchantId}/daily?date=${date}`,
        { signal },
      ),
    refetchInterval: 5_000,
    staleTime: 0,
  });
}
