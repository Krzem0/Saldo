using Saldo.Domain.Entities;

namespace Saldo.Application.DTOs;

/// <summary>Detached period and transaction data from one consistent database read.</summary>
public sealed record AccountingPeriodVerificationData(
    AccountingPeriod? Period, IReadOnlyList<Transaction> Transactions);
