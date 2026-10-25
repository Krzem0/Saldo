using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Saldo.Domain.Entities;
using Saldo.Domain.Enums;

namespace Saldo.Domain.ValueObjects;

public sealed record AccountingPeriodSnapshot(
    decimal IncomeTotal, decimal ExpenseTotal, int TransactionCount, string DataHash, int HashVersion)
{
    public const int CurrentHashVersion = 1;
    public decimal Balance => IncomeTotal - ExpenseTotal;

    public static AccountingPeriodSnapshot Calculate(CalendarMonth month,
        IReadOnlyCollection<Transaction> transactions, CancellationToken ct = default)
    {
        decimal income = 0, expense = 0;
        using var hash = SHA256.Create();
        using var stream = new CryptoStream(Stream.Null, hash, CryptoStreamMode.Write);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        // Version 1: length-prefixed UTF-8 strings, little-endian integers,
        // DateOnly day numbers, nullable-ID presence bits, sorted IDs, and
        // invariant G29 decimals (scale-independent). Never include dictionary names.
        writer.Write("Saldo.AccountingPeriodSnapshot");
        writer.Write(CurrentHashVersion);
        writer.Write(month.StartDate.DayNumber);
        writer.Write(month.EndDateExclusive.DayNumber);
        writer.Write(transactions.Count);
        foreach (var transaction in transactions.OrderBy(transaction => transaction.Id))
        {
            ct.ThrowIfCancellationRequested();
            if (transaction.Date < month.StartDate || transaction.Date >= month.EndDateExclusive)
                throw new InvalidOperationException("Transaction is outside the accounting period.");
            switch (transaction.Type)
            {
                case TransactionType.Income: income += transaction.Amount; break;
                case TransactionType.Expense: expense += transaction.Amount; break;
                default: throw new InvalidOperationException("Unknown transaction type.");
            }

            writer.Write(transaction.Id);
            writer.Write(transaction.Date.DayNumber);
            writer.Write((int)transaction.Type);
            writer.Write(transaction.Amount.ToString("G29", CultureInfo.InvariantCulture));
            writer.Write(transaction.CategoryId);
            WriteNullableId(writer, transaction.PayerId);
            WriteNullableId(writer, transaction.CounterpartyId);
            WriteNullableId(writer, transaction.LocationId);
            writer.Write(transaction.Description);
            writer.Write(transaction.Tags.Count);
            foreach (var tag in transaction.Tags.OrderBy(tag => tag.TagId))
            {
                ct.ThrowIfCancellationRequested();
                writer.Write(tag.TagId);
            }
        }

        writer.Flush();
        stream.FlushFinalBlock();
        ct.ThrowIfCancellationRequested();
        return new(income, expense, transactions.Count, Convert.ToHexStringLower(hash.Hash!), CurrentHashVersion);
    }

    private static void WriteNullableId(BinaryWriter writer, int? value)
    {
        writer.Write(value.HasValue);
        if (value is { } id) writer.Write(id);
    }
}
