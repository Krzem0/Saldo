using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Saldo.Application.DTOs;
using Saldo.Application.UseCases;
using Saldo.Domain.Entities;
using Saldo.Domain.Enums;
using Saldo.Domain.ValueObjects;
using Saldo.Infrastructure.Sqlite.Persistence;
using Saldo.Infrastructure.Sqlite.Repositories;
using Saldo.Tests.Integration.Helpers;

namespace Saldo.Tests.Integration.Repositories;

public sealed class AccountingPeriodSnapshotTests
{
    [Fact]
    public async Task Close_CapturesExactTotalsCountAndHash_AndVerifiesSuccessfully()
    {
        using var db = new TestDatabase(useMigrations: true);
        var repository = await SeedAsync(db.Context);

        await new CloseAccountingPeriod(repository).ExecuteAsync(2026, 1);
        var saved = (await repository.GetByMonthAsync(2026, 1))!;
        var result = await new VerifyAccountingPeriod(repository).ExecuteAsync(2026, 1);

        Assert.True(saved.IsClosed);
        Assert.Equal(1234.56m, saved.ClosedIncomeTotal);
        Assert.Equal(30.03m, saved.ClosedExpenseTotal);
        Assert.Equal(3, saved.ClosedTransactionCount);
        Assert.Equal(64, saved.ClosedDataHash!.Length);
        Assert.Equal(AccountingPeriodSnapshot.CurrentHashVersion, saved.HashVersion);
        Assert.Equal(AccountingPeriodVerificationStatus.Valid, result.Value.Status);
        Assert.Empty(result.Value.Differences);
        Assert.Equal(1204.53m, result.Value.StoredSnapshot!.Balance);
        Assert.Equal(result.Value.StoredSnapshot, result.Value.ActualSnapshot);
    }

    [Theory]
    [InlineData("amount", "UPDATE Transactions SET Amount = '40.04' WHERE Id = 2", true, false)]
    [InlineData("description", "UPDATE Transactions SET Description = 'Changed' WHERE Id = 2", false, false)]
    [InlineData("date", "UPDATE Transactions SET Date = '2026-01-20' WHERE Id = 2", false, false)]
    [InlineData("category", "UPDATE Transactions SET CategoryId = 2 WHERE Id = 2", false, false)]
    [InlineData("tags", "DELETE FROM TransactionTags WHERE TransactionId = 2", false, false)]
    [InlineData("delete", "DELETE FROM Transactions WHERE Id = 2", true, true)]
    [InlineData("move", "UPDATE Transactions SET Date = '2026-02-01' WHERE Id = 2", true, true)]
    [InlineData("offsetting", "UPDATE Transactions SET Amount = CASE Id WHEN 2 THEN '11.01' WHEN 3 THEN '19.02' ELSE Amount END", false, false)]
    [InlineData("insert", "INSERT INTO Transactions (Date, Type, Amount, CategoryId, Description) VALUES ('2026-01-12', 1, '1.01', 1, 'Added outside app')", true, true)]
    public async Task Verify_DetectsExternalChangesWithoutReplacingReference(string scenario, string sql,
        bool expenseChanged, bool countChanged)
    {
        using var db = new TestDatabase();
        var repository = await SeedAsync(db.Context);
        await new CloseAccountingPeriod(repository).ExecuteAsync(2026, 1);
        var before = (await repository.GetByMonthAsync(2026, 1))!;
        await db.Context.Database.ExecuteSqlRawAsync(sql);

        var result = await new VerifyAccountingPeriod(repository).ExecuteAsync(2026, 1);
        var after = (await repository.GetByMonthAsync(2026, 1))!;

        Assert.True(result.IsSuccess, scenario);
        Assert.Equal(AccountingPeriodVerificationStatus.Mismatch, result.Value.Status);
        Assert.Contains(AccountingPeriodDifference.DataHash, result.Value.Differences);
        Assert.Equal(expenseChanged, result.Value.Differences.Contains(AccountingPeriodDifference.ExpenseTotal));
        Assert.Equal(countChanged, result.Value.Differences.Contains(AccountingPeriodDifference.TransactionCount));
        Assert.DoesNotContain(AccountingPeriodDifference.IncomeTotal, result.Value.Differences);
        Assert.Equal(before.ClosedDataHash, after.ClosedDataHash);
        Assert.Equal(before.ClosedIncomeTotal, after.ClosedIncomeTotal);
        Assert.Equal(before.ClosedExpenseTotal, after.ClosedExpenseTotal);
        Assert.Equal(before.ClosedTransactionCount, after.ClosedTransactionCount);
        // A repeated close must not legitimize changed data.
        await new CloseAccountingPeriod(repository).ExecuteAsync(2026, 1);
        Assert.Equal(before.ClosedDataHash, (await repository.GetByMonthAsync(2026, 1))!.ClosedDataHash);
    }

    [Fact]
    public async Task RenamingDictionaryEntries_DoesNotInvalidateSnapshot()
    {
        using var db = new TestDatabase();
        var repository = await SeedAsync(db.Context);
        await new CloseAccountingPeriod(repository).ExecuteAsync(2026, 1);
        await db.Context.Database.ExecuteSqlRawAsync("UPDATE Categories SET Name = 'Renamed' WHERE Id = 1");
        await db.Context.Database.ExecuteSqlRawAsync("UPDATE Tags SET Name = 'Renamed' WHERE Id = 1");

        Assert.Equal(AccountingPeriodVerificationStatus.Valid,
            (await new VerifyAccountingPeriod(repository).ExecuteAsync(2026, 1)).Value.Status);
    }

    [Fact]
    public async Task ReopenRetainsOldSnapshot_AndClosingAgainCapturesChanges()
    {
        using var db = new TestDatabase();
        var repository = await SeedAsync(db.Context);
        await new CloseAccountingPeriod(repository).ExecuteAsync(2026, 1);
        var before = (await repository.GetByMonthAsync(2026, 1))!;
        await new ReopenAccountingPeriod(repository).ExecuteAsync(2026, 1);
        Assert.Equal(before.ClosedDataHash, (await repository.GetByMonthAsync(2026, 1))!.ClosedDataHash);
        Assert.Equal(AccountingPeriodVerificationStatus.Open,
            (await new VerifyAccountingPeriod(repository).ExecuteAsync(2026, 1)).Value.Status);
        await db.Context.Database.ExecuteSqlRawAsync("UPDATE Transactions SET Description = 'Approved change' WHERE Id = 2");

        await new CloseAccountingPeriod(repository).ExecuteAsync(2026, 1);
        var result = (await new VerifyAccountingPeriod(repository).ExecuteAsync(2026, 1)).Value;
        Assert.Equal(AccountingPeriodVerificationStatus.Valid, result.Status);
        Assert.NotEqual(before.ClosedDataHash, result.StoredSnapshot!.DataHash);
    }

    [Fact]
    public async Task LegacyClosedPeriodWithMissingSnapshot_IsReportedWithoutRepair()
    {
        using var db = new TestDatabase();
        var repository = await SeedAsync(db.Context);
        await db.Context.Database.ExecuteSqlRawAsync("UPDATE AccountingPeriods SET IsClosed = 1");

        var result = await new VerifyAccountingPeriod(repository).ExecuteAsync(2026, 1);
        await new CloseAccountingPeriod(repository).ExecuteAsync(2026, 1);

        Assert.Equal(AccountingPeriodVerificationStatus.MissingSnapshot, result.Value.Status);
        Assert.Null((await repository.GetByMonthAsync(2026, 1))!.ClosedDataHash);
    }

    [Fact]
    public async Task FailedSnapshotWrite_DoesNotPartiallyCloseOrPopulateSnapshot()
    {
        using var db = new TestDatabase();
        var repository = await SeedAsync(db.Context);
        await db.Context.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER RejectClosing BEFORE UPDATE ON AccountingPeriods
            WHEN NEW.IsClosed = 1 BEGIN SELECT RAISE(ABORT, 'Simulated closing failure'); END;
            """);

        await Assert.ThrowsAsync<SqliteException>(() => new CloseAccountingPeriod(repository).ExecuteAsync(2026, 1));

        var period = (await repository.GetByMonthAsync(2026, 1))!;
        Assert.False(period.IsClosed);
        Assert.Null(period.ClosedIncomeTotal);
        Assert.Null(period.ClosedExpenseTotal);
        Assert.Null(period.ClosedTransactionCount);
        Assert.Null(period.ClosedDataHash);
        Assert.Null(period.HashVersion);
    }

    [Fact]
    public async Task ConsistentRead_UsesOldSnapshotEvenIfExternalWriterChangesDataBetweenQueries()
    {
        using var db = new TestDatabase();
        await db.Context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
        var repository = await SeedAsync(db.Context);
        await new CloseAccountingPeriod(repository).ExecuteAsync(2026, 1);
        var interceptor = new ChangeAfterPeriodRead(async () =>
        {
            await using var writer = new SaldoDbContext(new DbContextOptionsBuilder<SaldoDbContext>()
                .UseSqlite(db.Context.Database.GetConnectionString()!).Options);
            await writer.Database.ExecuteSqlRawAsync("UPDATE Transactions SET Description = 'Concurrent change' WHERE Id = 2");
        });
        await using var reader = new SaldoDbContext(new DbContextOptionsBuilder<SaldoDbContext>()
            .UseSqlite(db.Context.Database.GetConnectionString()!).AddInterceptors(interceptor).Options);

        var result = await new VerifyAccountingPeriod(new AccountingPeriodRepository(reader)).ExecuteAsync(2026, 1);

        Assert.True(interceptor.HasChanged);
        Assert.Equal(AccountingPeriodVerificationStatus.Valid, result.Value.Status);
        Assert.Equal(AccountingPeriodVerificationStatus.Mismatch,
            (await new VerifyAccountingPeriod(repository).ExecuteAsync(2026, 1)).Value.Status);
    }

    private sealed class ChangeAfterPeriodRead(Func<Task> change) : DbCommandInterceptor
    {
        public bool HasChanged { get; private set; }
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,
            CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (!HasChanged && eventData.Context!.Database.CurrentTransaction is not null
                && command.CommandText.Contains("FROM \"AccountingPeriods\"", StringComparison.Ordinal))
            {
                HasChanged = true;
                await change();
            }
            return result;
        }
    }

    private static async Task<AccountingPeriodRepository> SeedAsync(SaldoDbContext context)
    {
        var category = new Category { Name = "Food" };
        var otherCategory = new Category { Name = "Other" };
        var tag = new Tag { Name = "Original tag" };
        context.AddRange(category, otherCategory, tag);
        await context.SaveChangesAsync();
        var transactions = new TransactionRepository(context);
        await transactions.AddAsync(new Transaction
        {
            Date = new DateOnly(2026, 1, 1), Amount = 1234.56m, Type = TransactionType.Income,
            CategoryId = category.Id, Description = "Income"
        });
        await transactions.AddAsync(new Transaction
        {
            Date = new DateOnly(2026, 1, 10), Amount = 10.01m, Type = TransactionType.Expense,
            CategoryId = category.Id, Description = "Expense", Tags = [new TransactionTag { TagId = tag.Id }]
        });
        await transactions.AddAsync(new Transaction
        {
            Date = new DateOnly(2026, 1, 31), Amount = 20.02m, Type = TransactionType.Expense,
            CategoryId = category.Id, Description = "Expense 2"
        });
        return new AccountingPeriodRepository(context);
    }
}
