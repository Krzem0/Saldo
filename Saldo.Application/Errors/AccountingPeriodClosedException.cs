using FluentResults;

namespace Saldo.Application.Errors;

/// <summary>Signals that the atomic persistence check rejected a write.</summary>
public sealed class AccountingPeriodClosedException(DateOnly date)
    : Exception("The accounting period is closed.")
{
    public DateOnly Date { get; } = date;

    public Error ToError() => new Error(ErrorCodes.AccountingPeriod.Closed)
        .WithMetadata("PropertyName", "Date")
        .WithMetadata("PeriodDate", Date);
}
