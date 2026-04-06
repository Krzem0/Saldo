using Saldo.Application.DTOs;
using Saldo.Application.Interfaces;
using Saldo.Application.Mapping;

namespace Saldo.Application.UseCases;

public sealed class ListTransactions
{
    private readonly ITransactionRepository _transactions;

    public ListTransactions(ITransactionRepository transactions)
    {
        _transactions = transactions;
    }

    public async Task<IReadOnlyList<TransactionDto>> ExecuteAsync(ListTransactionsQuery query, CancellationToken ct = default)
    {
        var transactions = await _transactions.GetByMonthAsync(query.Year, query.Month, ct);
        return transactions.Select(TransactionMapper.ToDto).ToList();
    }
}
