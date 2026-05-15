import type { Metadata } from "next";

import { DailyBalanceView } from "@/components/balances/daily-balance-view";
import { requireSession } from "@/lib/session";
import { toIsoDate } from "@/lib/utils";

export const metadata: Metadata = {
  title: "Consolidado diário — Cashflow",
};

export const dynamic = "force-dynamic";

export default async function DailyBalancePage() {
  const session = await requireSession("/balances/daily");

  return (
    <DailyBalanceView
      merchantId={session.merchantId}
      initialDate={toIsoDate(new Date())}
    />
  );
}
