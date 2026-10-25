using FluentResults;
using Saldo.Application.Errors;
using Saldo.Application.Interfaces;

namespace Saldo.Application.Validation;

internal static class AccountingPeriodProtection
{
    public static async Task<Error?> GetClosedErrorAsync(
        IAccountingPeriodRepository periods, CancellationToken ct, params DateOnly[] dates)
    {
        foreach (var date in dates.DistinctBy(date => (date.Year, date.Month)))
        {
            if (await periods.GetByMonthAsync(date.Year, date.Month, ct) is { IsClosed: true })
                return new AccountingPeriodClosedException(date).ToError();
        }

        return null;
    }
}
