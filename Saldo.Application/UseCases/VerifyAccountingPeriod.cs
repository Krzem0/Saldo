using FluentResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Saldo.Application.DTOs;
using Saldo.Application.Errors;
using Saldo.Application.Interfaces;
using Saldo.Domain.ValueObjects;

namespace Saldo.Application.UseCases;

public sealed class VerifyAccountingPeriod(IAccountingPeriodRepository periods, ILogger<VerifyAccountingPeriod>? logger = null)
{
    public async Task<Result<AccountingPeriodVerificationDto>> ExecuteAsync(int year, int month, CancellationToken ct = default)
    {
        CalendarMonth bounds;
        try
        {
            bounds = CalendarMonth.FromYearMonth(year, month);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Result.Fail<AccountingPeriodVerificationDto>(ErrorCodes.AccountingPeriod.InvalidMonth);
        }

        try
        {
            // Open/absent months need only an indexed state lookup, not a full
            // transaction scan. Closed months are re-read consistently below.
            if (await periods.GetByMonthAsync(year, month, ct) is not { IsClosed: true })
                return Result.Ok(new AccountingPeriodVerificationDto(year, month,
                    AccountingPeriodVerificationStatus.Open, [], null, null));

            // SQLite async reads can execute synchronously. Run both the dedicated
            // read and hashing off the caller's UI thread; do not share this scope
            // with other concurrent operations on its DbContext.
            return await Task.Run(async () =>
            {
                var data = await periods.ReadVerificationDataAsync(bounds, ct);
                if (data.Period is not { IsClosed: true } period)
                    return Result.Ok(new AccountingPeriodVerificationDto(year, month,
                        AccountingPeriodVerificationStatus.Open, [], null, null));

                var actual = AccountingPeriodSnapshot.Calculate(bounds, data.Transactions, ct);
                if (period.ClosedIncomeTotal is not { } income || period.ClosedExpenseTotal is not { } expense
                    || period.ClosedTransactionCount is not { } count || string.IsNullOrEmpty(period.ClosedDataHash)
                    || period.HashVersion is not { } version)
                    return Result.Ok(new AccountingPeriodVerificationDto(year, month,
                        AccountingPeriodVerificationStatus.MissingSnapshot, [], null, actual));

                var stored = new AccountingPeriodSnapshot(income, expense, count, period.ClosedDataHash, version);
                if (version != AccountingPeriodSnapshot.CurrentHashVersion)
                    return Result.Ok(new AccountingPeriodVerificationDto(year, month,
                        AccountingPeriodVerificationStatus.UnsupportedHashVersion, [], stored, actual));

                List<AccountingPeriodDifference> differences = [];
                if (stored.IncomeTotal != actual.IncomeTotal) differences.Add(AccountingPeriodDifference.IncomeTotal);
                if (stored.ExpenseTotal != actual.ExpenseTotal) differences.Add(AccountingPeriodDifference.ExpenseTotal);
                if (stored.TransactionCount != actual.TransactionCount) differences.Add(AccountingPeriodDifference.TransactionCount);
                if (!string.Equals(stored.DataHash, actual.DataHash, StringComparison.Ordinal))
                    differences.Add(AccountingPeriodDifference.DataHash);
                return Result.Ok(new AccountingPeriodVerificationDto(year, month,
                    differences.Count == 0 ? AccountingPeriodVerificationStatus.Valid : AccountingPeriodVerificationStatus.Mismatch,
                    differences, stored, actual));
            }, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            (logger ?? NullLogger<VerifyAccountingPeriod>.Instance).LogError(ex,
                "Failed to verify accounting period {Year}-{Month}.", year, month);
            return Result.Fail<AccountingPeriodVerificationDto>(ErrorCodes.AccountingPeriod.VerificationFailed);
        }
    }
}
