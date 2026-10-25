using System.Globalization;
using Saldo.Domain.Entities;
using Saldo.Domain.Enums;
using Saldo.Domain.ValueObjects;

namespace Saldo.Tests.Unit.Domain;

public sealed class AccountingPeriodSnapshotTests
{
    private static readonly CalendarMonth Month = CalendarMonth.FromYearMonth(2026, 1);

    [Fact]
    public void Calculate_EmptyMonth_HasZeroTotalsAndVersionedHash()
    {
        var snapshot = AccountingPeriodSnapshot.Calculate(Month, []);
        Assert.Equal(0m, snapshot.IncomeTotal);
        Assert.Equal(0m, snapshot.ExpenseTotal);
        Assert.Equal(0m, snapshot.Balance);
        Assert.Equal(0, snapshot.TransactionCount);
        Assert.Equal(64, snapshot.DataHash.Length);
        // Fixed version-1 fixture: detect accidental format changes without a version bump.
        Assert.Equal("306c143c3face6b8672e859f1a19a81508baa0d47263c20d7d5b4d05c2297fd5", snapshot.DataHash);
        Assert.Equal(AccountingPeriodSnapshot.CurrentHashVersion, snapshot.HashVersion);
        Assert.NotEqual(snapshot.DataHash,
            AccountingPeriodSnapshot.Calculate(CalendarMonth.FromYearMonth(2026, 2), []).DataHash);
    }

    [Fact]
    public void Calculate_IsStableAcrossCultureOrderingAndDecimalScale()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");
            var expense = Transaction();
            var income = Transaction();
            income.Id = 2;
            income.Type = TransactionType.Income;
            income.Amount = 100.00m;
            var expected = AccountingPeriodSnapshot.Calculate(Month, [expense, income]);

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            expense.Amount = 10.0m;
            income.Amount = 100m;
            expense.Tags = expense.Tags.Reverse().ToList();
            var actual = AccountingPeriodSnapshot.Calculate(Month, [income, expense]);

            Assert.Equal(expected, actual);
            Assert.Equal(100m, actual.IncomeTotal);
            Assert.Equal(10m, actual.ExpenseTotal);
            Assert.Equal(90m, actual.Balance);
            Assert.Equal(2, actual.TransactionCount);
        }
        finally { CultureInfo.CurrentCulture = originalCulture; }
    }

    [Theory]
    [InlineData("id")]
    [InlineData("date")]
    [InlineData("type")]
    [InlineData("amount")]
    [InlineData("category")]
    [InlineData("payer")]
    [InlineData("counterparty")]
    [InlineData("location")]
    [InlineData("description")]
    [InlineData("tag")]
    public void Calculate_ChangesHashForEveryStoredTransactionField(string field)
    {
        var transaction = Transaction();
        var before = AccountingPeriodSnapshot.Calculate(Month, [transaction]);
        switch (field)
        {
            case "id": transaction.Id++; break;
            case "date": transaction.Date = transaction.Date.AddDays(1); break;
            case "type": transaction.Type = TransactionType.Income; break;
            case "amount": transaction.Amount++; break;
            case "category": transaction.CategoryId++; break;
            case "payer": transaction.PayerId = null; break;
            case "counterparty": transaction.CounterpartyId++; break;
            case "location": transaction.LocationId = null; break;
            case "description": transaction.Description += " "; break;
            case "tag": transaction.Tags.First().TagId++; break;
        }
        Assert.NotEqual(before.DataHash, AccountingPeriodSnapshot.Calculate(Month, [transaction]).DataHash);
    }

    [Fact]
    public void Calculate_DictionaryRenamesDoNotChangeHash()
    {
        var transaction = Transaction();
        var before = AccountingPeriodSnapshot.Calculate(Month, [transaction]);
        transaction.Category = new Category { Id = transaction.CategoryId, Name = "Renamed", ColorCode = "#112233" };
        transaction.Payer = new Party { Id = transaction.PayerId!.Value, Name = "Renamed" };
        transaction.Counterparty = new Party { Id = transaction.CounterpartyId!.Value, Name = "Renamed" };
        transaction.Location = new Location { Id = transaction.LocationId!.Value, Name = "Renamed" };
        transaction.Tags.First().Tag = new Tag { Name = "Renamed" };
        Assert.Equal(before, AccountingPeriodSnapshot.Calculate(Month, [transaction]));
    }

    [Fact]
    public void Calculate_RejectsTransactionsOutsideMonthAndUnknownTypes()
    {
        var transaction = Transaction();
        transaction.Date = Month.EndDateExclusive;
        Assert.Throws<InvalidOperationException>(() => AccountingPeriodSnapshot.Calculate(Month, [transaction]));
        transaction.Date = Month.StartDate;
        transaction.Type = (TransactionType)99;
        Assert.Throws<InvalidOperationException>(() => AccountingPeriodSnapshot.Calculate(Month, [transaction]));
    }

    private static Transaction Transaction() => new()
    {
        Id = 1, Date = new DateOnly(2026, 1, 10), Amount = 10.00m, Type = TransactionType.Expense,
        CategoryId = 1, PayerId = 1, CounterpartyId = 2, LocationId = 3,
        Description = "Zakupy — żółć\n\"test\"",
        Tags = [new TransactionTag { TagId = 3 }, new TransactionTag { TagId = 7 }]
    };
}
