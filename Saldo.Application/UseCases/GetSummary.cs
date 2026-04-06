using Saldo.Application.DTOs;
using Saldo.Application.Interfaces;
using Saldo.Domain.Enums;

namespace Saldo.Application.UseCases;

public sealed class GetSummary
{
    private readonly ITransactionRepository _transactions;

    public GetSummary(ITransactionRepository transactions)
    {
        _transactions = transactions;
    }

    public async Task<MonthlySummaryDto> ExecuteAsync(ListTransactionsQuery query, CancellationToken ct = default)
    {
        var transactions = await _transactions.GetByMonthAsync(query.Year, query.Month, ct);

        var totalIncome = transactions
            .Where(t => t.Direction == TransactionDirection.Income)
            .Sum(t => t.Amount);

        var totalExpense = transactions
            .Where(t => t.Direction == TransactionDirection.Expense)
            .Sum(t => t.Amount);

        return new MonthlySummaryDto(query.Year, query.Month, totalIncome, totalExpense, totalIncome - totalExpense);
    }
}
