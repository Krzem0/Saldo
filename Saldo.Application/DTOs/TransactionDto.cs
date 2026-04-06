using Saldo.Domain.Enums;

namespace Saldo.Application.DTOs;

public sealed record TransactionDto(
    int Id,
    DateOnly Date,
    TransactionDirection Direction,
    decimal Amount,
    int CategoryId,
    string CategoryName,
    int PayerId,
    string PayerName,
    int CounterpartyId,
    string CounterpartyName,
    string? Description,
    string? Location,
    IReadOnlyList<string> Tags
);
