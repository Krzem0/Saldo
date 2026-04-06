using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saldo.Domain.Entities;

namespace Saldo.Infrastructure.Sqlite.Persistence.Configurations;

internal sealed class TransactionTagConfiguration : IEntityTypeConfiguration<TransactionTag>
{
    public void Configure(EntityTypeBuilder<TransactionTag> e)
    {
        e.ToTable("TransactionTags");

        e.HasKey(x => new { x.TransactionId, x.TagId });

        e.HasOne(x => x.Transaction)
            .WithMany(x => x.Tags)
            .HasForeignKey(x => x.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.Tag)
            .WithMany(x => x.Transactions)
            .HasForeignKey(x => x.TagId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasIndex(x => x.TagId);
    }
}
