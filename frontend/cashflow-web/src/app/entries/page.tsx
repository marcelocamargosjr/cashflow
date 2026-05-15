import type { Metadata } from "next";

import { EntriesView } from "@/components/entries/entries-view";
import { requireSession } from "@/lib/session";
import { toIsoDate } from "@/lib/utils";

export const metadata: Metadata = {
  title: "Lançamentos — Cashflow",
};

export const dynamic = "force-dynamic";

export default async function EntriesPage() {
  await requireSession("/entries");

  const today = new Date();
  const sevenDaysAgo = new Date();
  sevenDaysAgo.setDate(today.getDate() - 7);

  return (
    <EntriesView
      defaultFilters={{
        from: toIsoDate(sevenDaysAgo),
        to: toIsoDate(today),
        type: "",
        category: "",
        page: 1,
        size: 50,
      }}
    />
  );
}
