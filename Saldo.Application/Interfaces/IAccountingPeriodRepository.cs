using Saldo.Domain.Entities;
using Saldo.Domain.ValueObjects;

namespace Saldo.Application.Interfaces;

public interface IAccountingPeriodRepository
{
    Task<AccountingPeriod?> GetByMonthAsync(int year, int month, CancellationToken ct = default);
    // State changes must serialize with transaction writes. Reopening an absent
    // period is a no-op; closing one creates it without filling gaps.
    Task SetClosedAsync(CalendarMonth month, bool isClosed, CancellationToken ct = default);
}
