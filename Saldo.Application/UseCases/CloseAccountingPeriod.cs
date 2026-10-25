using FluentResults;
using Saldo.Application.Errors;
using Saldo.Application.Interfaces;
using Saldo.Domain.ValueObjects;

namespace Saldo.Application.UseCases;

public sealed class CloseAccountingPeriod(IAccountingPeriodRepository periods)
{
    public async Task<Result> ExecuteAsync(int year, int month, CancellationToken ct = default)
    {
        CalendarMonth bounds;
        try
        {
            bounds = CalendarMonth.FromYearMonth(year, month);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Result.Fail(ErrorCodes.AccountingPeriod.InvalidMonth);
        }

        // Capturing a snapshot includes SQLite reads and CPU work.
        await Task.Run(() => periods.SetClosedAsync(bounds, true, ct), ct);
        return Result.Ok();
    }
}
