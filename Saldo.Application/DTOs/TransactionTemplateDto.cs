using Saldo.Domain.Enums;

namespace Saldo.Application.DTOs;

public sealed record TransactionTemplateDto(
    int Id, string Name, TransactionType TransactionType, string? Description,
    int? CategoryId, int? PayerId, int? CounterpartyId, int? LocationId,
    decimal? Amount, IReadOnlyList<int> TagIds, string? CategoryColorCode = null);
