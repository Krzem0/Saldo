using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saldo.Domain.Entities;

namespace Saldo.Infrastructure.Sqlite.Persistence.Configurations;

internal sealed class TransactionTemplateTagConfiguration : IEntityTypeConfiguration<TransactionTemplateTag>
{
    public void Configure(EntityTypeBuilder<TransactionTemplateTag> e)
    {
        e.ToTable("TransactionTemplateTags");
        e.HasKey(x => new { x.TransactionTemplateId, x.TagId });
        e.HasOne(x => x.TransactionTemplate).WithMany(x => x.Tags)
            .HasForeignKey(x => x.TransactionTemplateId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Tag).WithMany().HasForeignKey(x => x.TagId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => x.TagId);
    }
}
