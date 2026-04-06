using Saldo.Application.Interfaces;

namespace Saldo.Application.UseCases;

public sealed class DeleteTransaction
{
    private readonly ITransactionRepository _transactions;

    public DeleteTransaction(ITransactionRepository transactions)
    {
        _transactions = transactions;
    }

    public async Task ExecuteAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0)
            throw new ArgumentException("Id must be positive.", nameof(id));

        var existing = await _transactions.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Transaction {id} not found.");

        await _transactions.DeleteAsync(existing.Id, ct);
    }
}
