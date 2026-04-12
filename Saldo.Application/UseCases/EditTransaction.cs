using Saldo.Application.DTOs;
using Saldo.Application.Errors;
using Saldo.Application.Interfaces;
using Saldo.Application.Mapping;
using Saldo.Domain.Entities;
using FluentResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Saldo.Application.UseCases;

public sealed class EditTransaction
{
    private readonly ITransactionRepository _transactions;
    private readonly ILogger<EditTransaction> _logger;

    public EditTransaction(ITransactionRepository transactions)
        : this(transactions, NullLogger<EditTransaction>.Instance)
    {
    }

    public EditTransaction(ITransactionRepository transactions, ILogger<EditTransaction> logger)
    {
        _transactions = transactions;
        _logger = logger;
    }

    public async Task<Result<TransactionDto>> ExecuteAsync(EditTransactionCommand command, CancellationToken ct = default)
    {
        _logger.LogDebug("Editing transaction {TransactionId}.", command.Id);

        if (command.Id <= 0)
        {
            _logger.LogWarning("Transaction edit rejected because id must be positive.");
            return Result.Fail<TransactionDto>(ErrorCodes.Transaction.IdMustBePositive);
        }
        if (command.Amount <= 0)
        {
            _logger.LogWarning("Transaction {TransactionId} edit rejected because amount must be positive.", command.Id);
            return Result.Fail<TransactionDto>(ErrorCodes.Transaction.AmountMustBePositive);
        }
        if (command.CategoryId <= 0)
        {
            _logger.LogWarning("Transaction {TransactionId} edit rejected because category id is missing.", command.Id);
            return Result.Fail<TransactionDto>(ErrorCodes.Transaction.CategoryRequired);
        }
        if (command.PayerId <= 0)
        {
            _logger.LogWarning("Transaction {TransactionId} edit rejected because payer id is missing.", command.Id);
            return Result.Fail<TransactionDto>(ErrorCodes.Transaction.PayerRequired);
        }
        if (command.CounterpartyId <= 0)
        {
            _logger.LogWarning("Transaction {TransactionId} edit rejected because counterparty id is missing.", command.Id);
            return Result.Fail<TransactionDto>(ErrorCodes.Transaction.CounterpartyRequired);
        }

        var existing = await _transactions.GetByIdAsync(command.Id, ct);
        if (existing is null)
        {
            _logger.LogWarning("Transaction {TransactionId} not found for edit.", command.Id);
            return Result.Fail<TransactionDto>(ErrorCodes.Transaction.NotFound);
        }

        existing.Date = command.Date;
        existing.Direction = command.Direction;
        existing.Amount = command.Amount;
        existing.CategoryId = command.CategoryId;
        existing.PayerId = command.PayerId;
        existing.CounterpartyId = command.CounterpartyId;
        existing.Description = command.Description;
        existing.Location = command.Location;
        existing.Tags = command.TagIds
            .Select(tagId => new TransactionTag { TagId = tagId })
            .ToList();

        await _transactions.UpdateAsync(existing, ct);

        var saved = await _transactions.GetByIdAsync(existing.Id, ct)
            ?? throw new InvalidOperationException($"Transaction {existing.Id} not found after update.");

        _logger.LogInformation("Transaction {TransactionId} edited successfully.", saved.Id);

        return Result.Ok(TransactionMapper.ToDto(saved));
    }
}
