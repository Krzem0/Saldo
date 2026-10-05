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
        t.Category?.ColorCode,
        t.PayerId,
        t.Payer?.Name,
        t.CounterpartyId,
        t.Counterparty?.Name,
        t.LocationId,
        t.Location?.Name,
        t.Description,
        t.Tags.OrderBy(tt => tt.TagId).Select(tt => tt.Tag?.Name ?? string.Empty).ToList(),
        t.Tags.OrderBy(tt => tt.TagId).Select(tt => tt.TagId).ToList()
    )
    {
        CategoryIconKey = t.Category?.IconKey,
        TagDetails = t.Tags.OrderBy(tt => tt.TagId)
            .Select(tt => new TransactionTagDto(tt.TagId, tt.Tag?.Name ?? string.Empty, tt.Tag?.ColorCode))
            .ToArray()
    };
}
