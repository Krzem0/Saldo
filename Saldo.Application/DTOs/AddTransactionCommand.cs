using Saldo.Domain.Enums;

namespace Saldo.Application.DTOs;

public sealed record AddTransactionCommand(
    DateOnly Date,
    TransactionDirection Direction,
    decimal Amount,
    int CategoryId,
    int PayerId,
    int CounterpartyId,
    string? Description,
    string? Location,
    IReadOnlyList<int> TagIds
);
