using Saldo.Domain.Enums;

namespace Saldo.Application.DTOs;

public sealed record TransactionDto(
    int Id,
    DateOnly Date,
    TransactionType Type,
    decimal Amount,
    int CategoryId,
    string CategoryName,
    string? CategoryColorCode,
    int PayerId,
    string PayerName,
    int CounterpartyId,
    string CounterpartyName,
    int? LocationId,
    string? Location,
    string? Description,
    IReadOnlyList<string> Tags,
    IReadOnlyList<int> TagIds
);
