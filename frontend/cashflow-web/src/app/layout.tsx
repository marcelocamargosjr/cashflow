import type { Metadata } from "next";

import "./globals.css";

import { NavBar } from "@/components/nav-bar";
import { Providers } from "@/components/providers";

export const metadata: Metadata = {
  title: "Cashflow — Painel do comerciante",
  description:
    "Sistema de fluxo de caixa para comerciantes: lançamentos diários e consolidado por categoria.",
  applicationName: "Cashflow",
};

export default function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="pt-BR" suppressHydrationWarning>
      <body className="min-h-screen bg-background text-foreground antialiased">
        <a
          href="#main"
          className="sr-only focus:not-sr-only focus:absolute focus:left-2 focus:top-2 focus:z-50 focus:rounded-md focus:bg-primary focus:px-3 focus:py-2 focus:text-sm focus:text-primary-foreground"
        >
          Pular para o conteúdo principal
        </a>
        <Providers>
          <NavBar />
          <main id="main" tabIndex={-1} className="container py-8">
            {children}
          </main>
        </Providers>
      </body>
    </html>
  );
}
