using Saldo.Domain.Enums;

namespace Saldo.Application.DTOs;

public interface ITransactionCommand
{
    DateOnly Date { get; }
    TransactionType Type { get; }
    decimal Amount { get; }
    int CategoryId { get; }
    int? PayerId { get; }
    string? PayerName { get; }
    int? CounterpartyId { get; }
    string? CounterpartyName { get; }
    string? Description { get; }
    string? Location { get; }
    IReadOnlyList<int> TagIds { get; }
}
