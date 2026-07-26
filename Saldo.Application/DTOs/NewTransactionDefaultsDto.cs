using Saldo.Domain.Enums;

namespace Saldo.Application.DTOs;

public sealed record NewTransactionDefaultsDto(
    DateOnly Date,
    TransactionType Type,
    int? PayerId
);
