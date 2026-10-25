using Saldo.Application.Interfaces;
using Saldo.Application.Errors;
using Saldo.Application.Validation;
using FluentResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Saldo.Application.UseCases;

public sealed class DeleteTransaction
{
    private readonly ITransactionRepository _transactions;
    private readonly IAccountingPeriodRepository _periods;
    private readonly ILogger<DeleteTransaction> _logger;

    public DeleteTransaction(ITransactionRepository transactions, IAccountingPeriodRepository periods)
        : this(transactions, periods, NullLogger<DeleteTransaction>.Instance)
    {
    }

    public DeleteTransaction(ITransactionRepository transactions, IAccountingPeriodRepository periods, ILogger<DeleteTransaction> logger)
    {
        _transactions = transactions;
        _periods = periods;
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

        if (await AccountingPeriodProtection.GetClosedErrorAsync(_periods, ct, existing.Date) is { } closedError)
            return Result.Fail(closedError);

        try
        {
            await _transactions.DeleteAsync(existing.Id, ct);
        }
        catch (AccountingPeriodClosedException ex)
        {
            return Result.Fail(ex.ToError());
        }

        _logger.LogInformation("Transaction {TransactionId} deleted successfully.", existing.Id);

        return Result.Ok();
    }
}
