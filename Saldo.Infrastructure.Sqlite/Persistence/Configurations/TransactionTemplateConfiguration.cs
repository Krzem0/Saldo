using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saldo.Domain.Entities;

namespace Saldo.Infrastructure.Sqlite.Persistence.Configurations;

internal sealed class TransactionTemplateConfiguration : IEntityTypeConfiguration<TransactionTemplate>
{
    public void Configure(EntityTypeBuilder<TransactionTemplate> e)
    {
        e.ToTable("TransactionTemplates");
        e.HasKey(x => x.Id);
        e.Property(x => x.Name).IsRequired().HasMaxLength(100);
        e.HasIndex(x => x.Name).IsUnique();
        e.Property(x => x.Description).IsRequired(false).HasMaxLength(500);
        e.Property(x => x.Amount).HasPrecision(18, 2);
        e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Payer).WithMany().HasForeignKey(x => x.PayerId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Counterparty).WithMany().HasForeignKey(x => x.CounterpartyId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Location).WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
    }
}
