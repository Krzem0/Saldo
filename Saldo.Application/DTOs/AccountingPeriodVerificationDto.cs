using Saldo.Domain.ValueObjects;

namespace Saldo.Application.DTOs;

public enum AccountingPeriodVerificationStatus
{
    Open,
    Valid,
    Mismatch,
    MissingSnapshot,
    UnsupportedHashVersion
}

public enum AccountingPeriodDifference
{
    IncomeTotal,
    ExpenseTotal,
    TransactionCount,
    DataHash
}

public sealed record AccountingPeriodVerificationDto(
    int Year,
    int Month,
    AccountingPeriodVerificationStatus Status,
    IReadOnlyList<AccountingPeriodDifference> Differences,
    AccountingPeriodSnapshot? StoredSnapshot,
    AccountingPeriodSnapshot? ActualSnapshot);
