using Saldo.Application.Interfaces;
using Saldo.Application.DTOs;
using Saldo.Domain.Entities;
using Saldo.Domain.ValueObjects;

namespace Saldo.Tests.Unit.Fakes;

internal sealed class FakeAccountingPeriodRepository : IAccountingPeriodRepository
{
    private readonly Dictionary<DateOnly, AccountingPeriod> _periods = [];
    public List<Transaction> Transactions { get; } = [];

    public Task<AccountingPeriod?> GetByMonthAsync(int year, int month, CancellationToken ct = default)
    {
        _periods.TryGetValue(CalendarMonth.FromYearMonth(year, month).StartDate, out var period);
        return Task.FromResult(period);
    }

    public Task SetClosedAsync(CalendarMonth month, bool isClosed, CancellationToken ct = default)
    {
        if (!_periods.TryGetValue(month.StartDate, out var period))
        {
            if (!isClosed) return Task.CompletedTask;
            period = new AccountingPeriod { StartDate = month.StartDate, EndDateExclusive = month.EndDateExclusive };
            _periods.Add(month.StartDate, period);
        }
        if (isClosed && !period.IsClosed)
        {
            var snapshot = AccountingPeriodSnapshot.Calculate(month, Transactions
                .Where(transaction => transaction.Date >= month.StartDate && transaction.Date < month.EndDateExclusive).ToList(), ct);
            period.ClosedIncomeTotal = snapshot.IncomeTotal;
            period.ClosedExpenseTotal = snapshot.ExpenseTotal;
            period.ClosedTransactionCount = snapshot.TransactionCount;
            period.ClosedDataHash = snapshot.DataHash;
            period.HashVersion = snapshot.HashVersion;
        }
        period.IsClosed = isClosed;
        return Task.CompletedTask;
    }

    public async Task<AccountingPeriodVerificationData> ReadVerificationDataAsync(CalendarMonth month, CancellationToken ct = default)
    {
        var period = await GetByMonthAsync(month.StartDate.Year, month.StartDate.Month, ct);
        return new(period, Transactions.Where(transaction => transaction.Date >= month.StartDate && transaction.Date < month.EndDateExclusive).ToList());
    }
}
