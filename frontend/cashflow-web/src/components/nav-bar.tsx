"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { signOut, useSession } from "next-auth/react";

import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

const links = [
  { href: "/entries", label: "Lançamentos" },
  { href: "/entries/new", label: "Novo lançamento" },
  { href: "/balances/daily", label: "Consolidado diário" },
];

export function NavBar() {
  const pathname = usePathname() ?? "";
  const { data: session, status } = useSession();
  const isAuthenticated = status === "authenticated";

  return (
    <header className="border-b bg-background">
      <nav
        className="container flex h-16 items-center justify-between gap-4"
        aria-label="Navegação principal"
      >
        <Link
          href="/"
          className="text-base font-semibold tracking-tight"
          aria-label="Cashflow — página inicial"
        >
          Cashflow
        </Link>
        <ul className="hidden items-center gap-1 md:flex" role="list">
          {links.map((link) => {
            const active = pathname.startsWith(link.href);
            return (
              <li key={link.href}>
                <Link
                  href={link.href}
                  className={cn(
                    "rounded-md px-3 py-2 text-sm font-medium text-muted-foreground transition-colors hover:text-foreground",
                    active && "bg-secondary text-foreground",
                  )}
                  aria-current={active ? "page" : undefined}
                >
                  {link.label}
                </Link>
              </li>
            );
          })}
        </ul>
        <div className="flex items-center gap-3">
          {isAuthenticated ? (
            <>
              <span className="hidden text-sm text-muted-foreground sm:inline" aria-live="polite">
                {session?.username ?? "Conectado"}
              </span>
              <Button
                variant="outline"
                size="sm"
                onClick={() => signOut({ callbackUrl: "/login" })}
              >
                Sair
              </Button>
            </>
          ) : (
            <Button asChild size="sm">
              <Link href="/login">Entrar</Link>
            </Button>
          )}
        </div>
      </nav>
    </header>
  );
}
