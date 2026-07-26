using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saldo.Domain.Entities;

namespace Saldo.Infrastructure.Sqlite.Persistence.Configurations;

internal sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> e)
    {
        e.ToTable("Transactions");

        e.HasKey(x => x.Id);

        e.Property(x => x.Date)
            .IsRequired();

        e.Property(x => x.Type)
            .IsRequired();

        e.Property(x => x.Amount)
            .IsRequired()
            .HasPrecision(18, 2);

        e.Property(x => x.Description)
            .HasMaxLength(500);

        // Indeksy pod filtrowanie i raporty
        e.HasIndex(x => x.Date);
        e.HasIndex(x => new { x.Date, x.Type });
        e.HasIndex(x => x.CategoryId);
        e.HasIndex(x => x.LocationId);
        e.HasIndex(x => x.PayerId);
        e.HasIndex(x => x.CounterpartyId);

        // Relationships (Restrict: nie pozwalaj kasować słowników, gdy istnieją transakcje)
        e.HasOne(x => x.Category)
            .WithMany(x => x.Transactions)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(x => x.Payer)
            .WithMany(x => x.PayerTransactions)
            .HasForeignKey(x => x.PayerId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(x => x.Counterparty)
            .WithMany(x => x.CounterpartyTransactions)
            .HasForeignKey(x => x.CounterpartyId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(x => x.Location)
            .WithMany(x => x.Transactions)
            .HasForeignKey(x => x.LocationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
