export type EntryType = "Credit" | "Debit";
export type EntryStatus = "Confirmed" | "Reversed";

export type Money = {
  value: number;
  currency: "BRL";
};

export type Entry = {
  id: string;
  merchantId: string;
  type: EntryType;
  amount: Money;
  description: string;
  category: string | null;
  entryDate: string;
  status: EntryStatus;
  createdAt: string;
  updatedAt?: string | null;
};

export type EntriesPage = {
  items: Entry[];
  page: number;
  size: number;
  total: number;
  hasNext: boolean;
};

export type EntriesFilters = {
  from: string;
  to: string;
  type?: EntryType | "";
  category?: string;
  page: number;
  size: number;
};

export type DailyBalanceCategory = {
  category: string;
  credit: number;
  debit: number;
  count: number;
};

export type DailyBalance = {
  merchantId: string;
  date: string;
  totalCredits: number;
  totalDebits: number;
  balance: number;
  entriesCount: number;
  byCategory: DailyBalanceCategory[];
  lastUpdatedAt: string;
  revision: number;
  cache?: { hit: boolean; ageSeconds: number };
};
