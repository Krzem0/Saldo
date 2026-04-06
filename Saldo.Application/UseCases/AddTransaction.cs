using Saldo.Application.DTOs;
using Saldo.Application.Interfaces;
using Saldo.Domain.Entities;

namespace Saldo.Application.UseCases;

public sealed class AddTransaction
{
    private readonly ITransactionRepository _transactions;

    public AddTransaction(ITransactionRepository transactions)
    {
        _transactions = transactions;
    }

    public async Task<TransactionDto> ExecuteAsync(AddTransactionCommand command, CancellationToken ct = default)
    {
        if (command.Amount <= 0)
            throw new ArgumentException("Amount must be positive.", nameof(command));
        if (command.CategoryId <= 0)
            throw new ArgumentException("CategoryId is required.", nameof(command));
        if (command.PayerId <= 0)
            throw new ArgumentException("PayerId is required.", nameof(command));
        if (command.CounterpartyId <= 0)
            throw new ArgumentException("CounterpartyId is required.", nameof(command));

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

        return ToDto(saved);
    }

    internal static TransactionDto ToDto(Transaction t) => new(
        t.Id,
        t.Date,
        t.Direction,
        t.Amount,
        t.CategoryId,
        t.Category?.Name ?? string.Empty,
        t.PayerId,
        t.Payer?.Name ?? string.Empty,
        t.CounterpartyId,
        t.Counterparty?.Name ?? string.Empty,
        t.Description,
        t.Location,
        t.Tags.Select(tt => tt.Tag?.Name ?? string.Empty).ToList()
    );
}
