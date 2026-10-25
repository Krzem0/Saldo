using Saldo.Domain.ValueObjects;

namespace Saldo.Tests.Unit.Domain;

public sealed class CalendarMonthTests
{
    [Theory]
    [InlineData(2026, 1, 31, 2026, 2)]
    [InlineData(2024, 2, 29, 2024, 3)]
    [InlineData(2025, 2, 28, 2025, 3)]
    [InlineData(2026, 12, 31, 2027, 1)]
    public void FromDate_UsesFirstDayAndExclusiveNextMonth(int year, int month, int day, int endYear, int endMonth)
    {
        var period = CalendarMonth.FromDate(new DateOnly(year, month, day));

        Assert.Equal(new DateOnly(year, month, 1), period.StartDate);
        Assert.Equal(new DateOnly(endYear, endMonth, 1), period.EndDateExclusive);
        Assert.Equal(CalendarMonth.FromYearMonth(year, month), period);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(2026, 0)]
    [InlineData(2026, 13)]
    [InlineData(9999, 12)] // No representable DateOnly exclusive end.
    public void FromYearMonth_UnrepresentableMonth_RejectsInput(int year, int month)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CalendarMonth.FromYearMonth(year, month));
    }
}
