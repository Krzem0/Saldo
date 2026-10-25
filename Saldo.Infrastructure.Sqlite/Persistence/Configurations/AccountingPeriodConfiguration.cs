using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saldo.Domain.Entities;

namespace Saldo.Infrastructure.Sqlite.Persistence.Configurations;

internal sealed class AccountingPeriodConfiguration : IEntityTypeConfiguration<AccountingPeriod>
{
    public void Configure(EntityTypeBuilder<AccountingPeriod> e)
    {
        // Exact calendar-month bounds plus a unique start also prevent overlaps.
        e.ToTable("AccountingPeriods", table => table.HasCheckConstraint(
            "CK_AccountingPeriods_CalendarMonth",
            "date(StartDate, 'start of month') IS NOT NULL " +
            "AND StartDate = date(StartDate, 'start of month') " +
            "AND date(StartDate, '+1 month') IS NOT NULL " +
            "AND EndDateExclusive = date(StartDate, '+1 month') " +
            "AND StartDate < EndDateExclusive"));

        e.HasKey(x => x.Id);
        e.HasIndex(x => x.StartDate).IsUnique();
        e.Property(x => x.StartDate).IsRequired();
        e.Property(x => x.EndDateExclusive).IsRequired();
        e.Property(x => x.IsClosed).IsRequired();
        e.Property(x => x.ClosedIncomeTotal).HasPrecision(18, 2);
        e.Property(x => x.ClosedExpenseTotal).HasPrecision(18, 2);
        e.Property(x => x.ClosedDataHash).HasMaxLength(64);
    }
}
