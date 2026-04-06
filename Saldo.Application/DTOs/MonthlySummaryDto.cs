namespace Saldo.Application.DTOs;

public sealed record MonthlySummaryDto(
    int Year,
    int Month,
    decimal TotalIncome,
    decimal TotalExpense,
    decimal Balance
);
