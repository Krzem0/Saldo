using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saldo.Domain.Entities;

namespace Saldo.Infrastructure.Sqlite.Persistence.Configurations;

internal sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> e)
    {
        e.ToTable("Tags");

        e.HasKey(x => x.Id);

        e.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(50);

        e.Property(x => x.IconKey).HasMaxLength(100);

        e.Property(x => x.ColorCode).HasMaxLength(7);

        e.HasIndex(x => x.Name)
            .IsUnique();
    }
}
