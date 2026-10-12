using Saldo.Domain.Enums;

namespace Saldo.Application.DTOs;

public sealed record SaveTransactionTemplateCommand(
    string Name,
    TransactionType TransactionType,
    string? Description = null,
    int? CategoryId = null,
    int? PayerId = null,
    int? CounterpartyId = null,
    int? LocationId = null,
    decimal? Amount = null,
    IReadOnlyList<int>? TagIds = null);
