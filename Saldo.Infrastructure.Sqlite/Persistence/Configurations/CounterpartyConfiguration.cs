using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saldo.Domain.Entities;

namespace Saldo.Infrastructure.Sqlite.Persistence.Configurations;

internal sealed class CounterpartyConfiguration : IEntityTypeConfiguration<Counterparty>
{
    public void Configure(EntityTypeBuilder<Counterparty> e)
    {
        e.ToTable("Counterparties");

        e.HasKey(x => x.Id);

        e.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        e.HasIndex(x => x.Name)
            .IsUnique();
    }
}
