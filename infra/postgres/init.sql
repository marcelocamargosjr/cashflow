-- Postgres init script — runs once on container first boot (mounted via docker-compose).
-- Cria o database do Ledger e os schemas referenciados nas migrations (`ledger`, `messaging`).
-- A criação de tabelas fica a cargo do EF Core (ver §05-DADOS.md §1.5).

-- Database principal do Ledger.
SELECT 'CREATE DATABASE cashflow_ledger'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'cashflow_ledger')
\gexec

\c cashflow_ledger

CREATE SCHEMA IF NOT EXISTS ledger;
CREATE SCHEMA IF NOT EXISTS messaging;
