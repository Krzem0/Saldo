using Saldo.Application.Interfaces;
using FluentResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Saldo.Application.UseCases;

public sealed class DeleteTransaction
{
    private readonly ITransactionRepository _transactions;
    private readonly ILogger<DeleteTransaction> _logger;

    public DeleteTransaction(ITransactionRepository transactions)
        : this(transactions, NullLogger<DeleteTransaction>.Instance)
    {
    }

    public DeleteTransaction(ITransactionRepository transactions, ILogger<DeleteTransaction> logger)
    {
        _transactions = transactions;
        _logger = logger;
    }

    public async Task<Result> ExecuteAsync(int id, CancellationToken ct = default)
    {
        _logger.LogDebug("Deleting transaction {TransactionId}.", id);

        if (id <= 0)
        {
            _logger.LogWarning("Transaction delete rejected because id must be positive.");
            return Result.Fail("Id must be positive.");
        }

        var existing = await _transactions.GetByIdAsync(id, ct);
        if (existing is null)
        {
            _logger.LogWarning("Transaction {TransactionId} not found for delete.", id);
            return Result.Fail($"Transaction {id} not found.");
        }

        await _transactions.DeleteAsync(existing.Id, ct);

        _logger.LogInformation("Transaction {TransactionId} deleted successfully.", existing.Id);

        return Result.Ok();
    }
}
