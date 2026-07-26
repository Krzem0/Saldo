using Saldo.Domain.Enums;

namespace Saldo.Application.DTOs;

public sealed record EditTransactionCommand(
    int Id,
    DateOnly Date,
    TransactionType Type,
    decimal Amount,
    int CategoryId,
    int? PayerId,
    string? PayerName,
    int? CounterpartyId,
    string? CounterpartyName,
    string? Description,
    string? Location,
    IReadOnlyList<int> TagIds
);
