using Saldo.Application.DTOs;
using Saldo.Domain.Entities;

namespace Saldo.Application.Mapping;

internal static class TransactionMapper
{
    internal static TransactionDto ToDto(Transaction t) => new(
        t.Id,
        t.Date,
        t.Type,
        t.Amount,
        t.CategoryId,
        t.Category?.Name ?? string.Empty,
        t.PayerId,
        t.Payer?.Name ?? string.Empty,
        t.CounterpartyId,
        t.Counterparty?.Name ?? string.Empty,
        t.LocationId,
        t.Location?.Name,
        t.Description,
        t.Tags.Select(tt => tt.Tag?.Name ?? string.Empty).ToList()
    );
}
