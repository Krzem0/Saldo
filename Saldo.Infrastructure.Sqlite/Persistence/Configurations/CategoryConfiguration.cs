using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saldo.Domain.Entities;

namespace Saldo.Infrastructure.Sqlite.Persistence.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> e)
    {
        e.ToTable("Categories");

        e.HasKey(x => x.Id);

        e.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        e.HasIndex(x => x.Name)
            .IsUnique();
    }
}
