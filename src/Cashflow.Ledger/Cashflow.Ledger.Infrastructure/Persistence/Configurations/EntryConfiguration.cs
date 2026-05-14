using Cashflow.Ledger.Domain.Entries;
using Cashflow.SharedKernel.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cashflow.Ledger.Infrastructure.Persistence.Configurations;

internal sealed class EntryConfiguration : IEntityTypeConfiguration<Entry>
{
    public void Configure(EntityTypeBuilder<Entry> builder)
    {
        builder.ToTable("entries", "ledger", t =>
            t.HasCheckConstraint("ck_entry_amount_positive", "amount_value > 0"));

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.MerchantId)
            .HasColumnName("merchant_id")
            .IsRequired();

        builder.Property(e => e.Type)
            .HasColumnName("entry_type")
            .HasConversion<short>()
            .IsRequired();

        // ComplexProperty (EF Core 8+) maps the Money record-struct in-place — no separate table.
        builder.ComplexProperty(e => e.Amount, money =>
        {
            money.Property(m => m.Value)
                .HasColumnName("amount_value")
                .HasColumnType("numeric(18,2)")
                .IsRequired();

            money.Property(m => m.Currency)
                .HasColumnName("amount_currency")
                .HasConversion<short>()
                .HasDefaultValue(Currency.BRL)
                .IsRequired();
        });

        builder.Property(e => e.Description)
            .HasColumnName("description")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Category)
            .HasColumnName("category")
            .HasMaxLength(50);

        builder.Property(e => e.EntryDate)
            .HasColumnName("entry_date")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasConversion<short>()
            .HasDefaultValue(EntryStatus.Confirmed)
            .IsRequired();

        builder.Property(e => e.ReversalReason)
            .HasColumnName("reversal_reason")
            .HasMaxLength(500);

        builder.Property(e => e.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .IsRequired();

        builder.Property(e => e.IdempotencyBodyHash)
            .HasColumnName("idempotency_body_hash")
            .HasColumnType("char(64)")
            .IsFixedLength()
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Ignore(e => e.DomainEvents);

        builder.HasIndex(e => new { e.MerchantId, e.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("uq_entry_idempotency");

        builder.HasIndex(e => new { e.MerchantId, e.EntryDate })
            .HasDatabaseName("ix_entries_merchant_date")
            .IsDescending(false, true);
    }
}
