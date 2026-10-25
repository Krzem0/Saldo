using Microsoft.EntityFrameworkCore;
using Saldo.Application.Interfaces;
using Saldo.Domain.Entities;
using Saldo.Domain.ValueObjects;
using Saldo.Infrastructure.Sqlite.Persistence;

namespace Saldo.Infrastructure.Sqlite.Repositories;

public sealed class AccountingPeriodRepository(SaldoDbContext context) : IAccountingPeriodRepository
{
    public Task<AccountingPeriod?> GetByMonthAsync(int year, int month, CancellationToken ct = default)
    {
        var bounds = CalendarMonth.FromYearMonth(year, month);
        return context.AccountingPeriods.AsNoTracking()
            .SingleOrDefaultAsync(period => period.StartDate == bounds.StartDate, ct);
    }

    public async Task SetClosedAsync(CalendarMonth month, bool isClosed, CancellationToken ct = default)
    {
        // SQLite's non-deferred write transaction serializes this operation with
        // transaction repositories, including their final closed-period checks.
        await using var databaseTransaction = await context.Database.BeginTransactionAsync(ct);
        if (isClosed)
        {
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO AccountingPeriods (StartDate, EndDateExclusive, IsClosed)
                VALUES ({month.StartDate}, {month.EndDateExclusive}, 1)
                ON CONFLICT (StartDate) DO UPDATE SET IsClosed = 1;
                """, ct);
        }
        else
        {
            // Preserve the closing snapshot. An absent month is already open.
            await context.AccountingPeriods.Where(period => period.StartDate == month.StartDate)
                .ExecuteUpdateAsync(update => update.SetProperty(period => period.IsClosed, false), ct);
        }

        await databaseTransaction.CommitAsync(ct);
    }
}
