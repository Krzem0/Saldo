using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saldo.Domain.Entities;

namespace Saldo.Infrastructure.Sqlite.Persistence.Configurations;

internal sealed class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> e)
    {
        e.ToTable("Members");

        e.HasKey(x => x.Id);

        e.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        e.HasIndex(x => x.Name)
            .IsUnique();
    }
}
