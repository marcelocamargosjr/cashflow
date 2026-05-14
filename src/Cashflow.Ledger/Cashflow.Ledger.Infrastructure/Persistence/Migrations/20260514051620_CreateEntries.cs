using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cashflow.Ledger.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreateEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ledger");

            migrationBuilder.CreateTable(
                name: "entries",
                schema: "ledger",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_type = table.Column<short>(type: "smallint", nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    entry_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    idempotency_key = table.Column<Guid>(type: "uuid", nullable: false),
                    idempotency_body_hash = table.Column<string>(type: "char(64)", fixedLength: true, maxLength: 64, nullable: false),
                    reversal_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    amount_currency = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)986),
                    amount_value = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entries", x => x.id);
                    table.CheckConstraint("ck_entry_amount_positive", "amount_value > 0");
                });

            migrationBuilder.CreateIndex(
                name: "ix_entries_merchant_date",
                schema: "ledger",
                table: "entries",
                columns: new[] { "merchant_id", "entry_date" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "uq_entry_idempotency",
                schema: "ledger",
                table: "entries",
                columns: new[] { "merchant_id", "idempotency_key" },
                unique: true);

            // Índice parcial: somente entries com status = 2 (Reversed) entram no índice.
            // EF Core não tem API nativa para partial index, daí o SQL cru — ver 05-DADOS.md §1.3.
            migrationBuilder.Sql(
                "CREATE INDEX ix_entries_reversed ON ledger.entries (status) WHERE status = 2;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ledger.ix_entries_reversed;");

            migrationBuilder.DropTable(
                name: "entries",
                schema: "ledger");

            // Schema dropado por simetria — só sobrevive se houver outras tabelas (ex.: messaging).
            migrationBuilder.Sql("DROP SCHEMA IF EXISTS ledger CASCADE;");
        }
    }
}
