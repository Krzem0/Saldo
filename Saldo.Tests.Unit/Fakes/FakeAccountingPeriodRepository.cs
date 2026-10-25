using Saldo.Application.Interfaces;
using Saldo.Domain.Entities;
using Saldo.Domain.ValueObjects;

namespace Saldo.Tests.Unit.Fakes;

internal sealed class FakeAccountingPeriodRepository : IAccountingPeriodRepository
{
    private readonly Dictionary<DateOnly, AccountingPeriod> _periods = [];

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
        period.IsClosed = isClosed;
        return Task.CompletedTask;
    }
}
