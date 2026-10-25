namespace Saldo.Domain.Entities;

public sealed class AccountingPeriod
{
    public int Id { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDateExclusive { get; set; }
    public bool IsClosed { get; set; }

    // Captured atomically when closing an open period, never by transaction writes.
    public decimal? ClosedIncomeTotal { get; set; }
    public decimal? ClosedExpenseTotal { get; set; }
    public int? ClosedTransactionCount { get; set; }
    public string? ClosedDataHash { get; set; }
    public int? HashVersion { get; set; }
}
