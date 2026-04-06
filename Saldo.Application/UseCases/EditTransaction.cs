using Saldo.Application.DTOs;
using Saldo.Application.Interfaces;
using Saldo.Application.Mapping;
using Saldo.Domain.Entities;

namespace Saldo.Application.UseCases;

public sealed class EditTransaction
{
    private readonly ITransactionRepository _transactions;

    public EditTransaction(ITransactionRepository transactions)
    {
        _transactions = transactions;
    }

    public async Task<TransactionDto> ExecuteAsync(EditTransactionCommand command, CancellationToken ct = default)
    {
        if (command.Id <= 0)
            throw new ArgumentException("Id must be positive.", nameof(command));
        if (command.Amount <= 0)
            throw new ArgumentException("Amount must be positive.", nameof(command));
        if (command.CategoryId <= 0)
            throw new ArgumentException("CategoryId is required.", nameof(command));
        if (command.PayerId <= 0)
            throw new ArgumentException("PayerId is required.", nameof(command));
        if (command.CounterpartyId <= 0)
            throw new ArgumentException("CounterpartyId is required.", nameof(command));

        var existing = await _transactions.GetByIdAsync(command.Id, ct)
            ?? throw new KeyNotFoundException($"Transaction {command.Id} not found.");

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

        return TransactionMapper.ToDto(saved);
    }
}
