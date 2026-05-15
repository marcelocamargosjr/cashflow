"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { signOut, useSession } from "next-auth/react";
import { LogOut, User } from "lucide-react";

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

  // Quando não autenticado, mostramos apenas a marca + botão Entrar:
  // nav links levariam a páginas guardadas, redirecionando para /login
  // — só ruído visual.
  return (
    <header className="border-b bg-background">
      <nav
        className="container flex h-16 items-center justify-between gap-4"
        aria-label="Navegação principal"
      >
        <Link
          href={isAuthenticated ? "/entries" : "/login"}
          className="flex items-center gap-2 text-base font-semibold tracking-tight"
          aria-label="Cashflow — página inicial"
        >
          <span aria-hidden="true" className="inline-block h-7 w-7 rounded-md bg-primary text-primary-foreground">
            <span className="flex h-full w-full items-center justify-center text-sm font-bold">$</span>
          </span>
          Cashflow
        </Link>
        {isAuthenticated ? (
          <ul className="hidden items-center gap-1 md:flex" role="list">
            {links.map((link) => {
              const active =
                link.href === "/entries"
                  ? pathname === "/entries"
                  : pathname.startsWith(link.href);
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
        ) : null}
        <div className="flex items-center gap-3">
          {isAuthenticated ? (
            <>
              <span
                className="hidden items-center gap-2 text-sm text-muted-foreground sm:inline-flex"
                aria-live="polite"
              >
                <User className="h-4 w-4" aria-hidden="true" />
                {session?.username ?? "Conta autenticada"}
              </span>
              <Button
                variant="outline"
                size="sm"
                onClick={() => signOut({ callbackUrl: "/login" })}
              >
                <LogOut className="mr-2 h-4 w-4" aria-hidden="true" />
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
