using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Saldo.Application.DTOs;
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
            var existing = await GetByMonthAsync(month.StartDate.Year, month.StartDate.Month, ct);
            // Never replace the reference snapshot by repeatedly closing a month.
            if (existing is { IsClosed: true }) return;
            var transactions = await ReadTransactionsAsync(month, ct);
            var snapshot = AccountingPeriodSnapshot.Calculate(month, transactions, ct);
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO AccountingPeriods (StartDate, EndDateExclusive, IsClosed,
                    ClosedIncomeTotal, ClosedExpenseTotal, ClosedTransactionCount, ClosedDataHash, HashVersion)
                VALUES ({month.StartDate}, {month.EndDateExclusive}, 1,
                    {snapshot.IncomeTotal}, {snapshot.ExpenseTotal}, {snapshot.TransactionCount}, {snapshot.DataHash}, {snapshot.HashVersion})
                ON CONFLICT (StartDate) DO UPDATE SET IsClosed = 1,
                    ClosedIncomeTotal = excluded.ClosedIncomeTotal,
                    ClosedExpenseTotal = excluded.ClosedExpenseTotal,
                    ClosedTransactionCount = excluded.ClosedTransactionCount,
                    ClosedDataHash = excluded.ClosedDataHash,
                    HashVersion = excluded.HashVersion;
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

    public async Task<AccountingPeriodVerificationData> ReadVerificationDataAsync(CalendarMonth month, CancellationToken ct = default)
    {
        await context.Database.OpenConnectionAsync(ct);
        try
        {
            // Deferred read transaction: period, transactions, and tags come from
            // one snapshot without taking the write reservation used for closing.
            using var sqliteTransaction = ((SqliteConnection)context.Database.GetDbConnection()).BeginTransaction(deferred: true);
            await using var databaseTransaction = await context.Database.UseTransactionAsync(sqliteTransaction, ct);
            var period = await GetByMonthAsync(month.StartDate.Year, month.StartDate.Month, ct);
            var transactions = period is { IsClosed: true } ? await ReadTransactionsAsync(month, ct) : [];
            await databaseTransaction!.CommitAsync(ct);
            return new(period, transactions);
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private Task<List<Transaction>> ReadTransactionsAsync(CalendarMonth month, CancellationToken ct) =>
        context.Transactions.AsNoTracking()
            .Where(transaction => transaction.Date >= month.StartDate && transaction.Date < month.EndDateExclusive)
            .Include(transaction => transaction.Tags).AsSingleQuery().ToListAsync(ct);
}
