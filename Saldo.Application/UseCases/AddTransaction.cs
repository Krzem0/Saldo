using Saldo.Application.DTOs;
using Saldo.Application.Errors;
using Saldo.Application.Interfaces;
using Saldo.Application.Mapping;
using Saldo.Domain.Entities;
using FluentResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Saldo.Application.UseCases;

public sealed class AddTransaction
{
    private readonly ITransactionRepository _transactions;
    private readonly ILogger<AddTransaction> _logger;

    public AddTransaction(ITransactionRepository transactions)
        : this(transactions, NullLogger<AddTransaction>.Instance)
    {
    }

    public AddTransaction(ITransactionRepository transactions, ILogger<AddTransaction> logger)
    {
        _transactions = transactions;
        _logger = logger;
    }

    public async Task<Result<TransactionDto>> ExecuteAsync(AddTransactionCommand command, CancellationToken ct = default)
    {
        _logger.LogDebug("Adding transaction for {Date} with amount {Amount}.", command.Date, command.Amount);

        if (command.Amount <= 0)
        {
            _logger.LogWarning("Transaction rejected because amount must be positive.");
            return Result.Fail<TransactionDto>(ErrorCodes.Transaction.AmountMustBePositive);
        }
        if (command.CategoryId <= 0)
        {
            _logger.LogWarning("Transaction rejected because category id is missing.");
            return Result.Fail<TransactionDto>(ErrorCodes.Transaction.CategoryRequired);
        }
        if (command.PayerId <= 0)
        {
            _logger.LogWarning("Transaction rejected because payer id is missing.");
            return Result.Fail<TransactionDto>(ErrorCodes.Transaction.PayerRequired);
        }
        if (command.CounterpartyId <= 0)
        {
            _logger.LogWarning("Transaction rejected because counterparty id is missing.");
            return Result.Fail<TransactionDto>(ErrorCodes.Transaction.CounterpartyRequired);
        }

        var transaction = new Transaction
        {
            Date = command.Date,
            Direction = command.Direction,
            Amount = command.Amount,
            CategoryId = command.CategoryId,
            PayerId = command.PayerId,
            CounterpartyId = command.CounterpartyId,
            Description = command.Description,
            Location = command.Location,
            Tags = command.TagIds
                .Select(tagId => new TransactionTag { TagId = tagId })
                .ToList()
        };

        await _transactions.AddAsync(transaction, ct);

        var saved = await _transactions.GetByIdAsync(transaction.Id, ct)
            ?? throw new InvalidOperationException($"Transaction {transaction.Id} not found after insert.");

        _logger.LogInformation("Transaction {TransactionId} added successfully.", saved.Id);

        return Result.Ok(TransactionMapper.ToDto(saved));
    }
}
