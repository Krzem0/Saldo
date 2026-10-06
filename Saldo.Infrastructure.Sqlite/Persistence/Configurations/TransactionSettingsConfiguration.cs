using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saldo.Domain.Entities;

namespace Saldo.Infrastructure.Sqlite.Persistence.Configurations;

internal sealed class TransactionSettingsConfiguration : IEntityTypeConfiguration<TransactionSettings>
{
    public void Configure(EntityTypeBuilder<TransactionSettings> e)
    {
        e.ToTable("TransactionSettings", table => table.HasCheckConstraint("CK_TransactionSettings_Singleton", "Id = 1"));
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.HasOne<Party>().WithMany().HasForeignKey(x => x.DefaultPayerId).OnDelete(DeleteBehavior.SetNull);
    }
}
