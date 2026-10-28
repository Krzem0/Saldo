using Saldo.Application.DTOs;
using Saldo.Domain.Entities;

namespace Saldo.Application.Mapping;

public static class TransactionTemplateMapper
{
    public static TransactionTemplateDto ToDto(TransactionTemplate template) => new(
        template.Id, template.Name, template.TransactionType, template.Description,
        template.CategoryId, template.PayerId, template.CounterpartyId, template.LocationId,
        template.Amount, template.Tags.Select(link => link.TagId).Order().ToArray(), template.Category?.ColorCode);
}
