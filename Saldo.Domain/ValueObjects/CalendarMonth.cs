namespace Saldo.Domain.ValueObjects;

/// <summary>
/// Calendar-month boundaries: inclusive start and exclusive end.
/// Calculating a month does not create a persisted accounting period.
/// </summary>
public sealed record CalendarMonth
{
    public DateOnly StartDate { get; }
    public DateOnly EndDateExclusive { get; }

    private CalendarMonth(DateOnly startDate)
    {
        StartDate = startDate;
        EndDateExclusive = startDate.AddMonths(1);
    }

    public static CalendarMonth FromYearMonth(int year, int month) =>
        new(new DateOnly(year, month, 1));

    public static CalendarMonth FromDate(DateOnly date) => FromYearMonth(date.Year, date.Month);
}
