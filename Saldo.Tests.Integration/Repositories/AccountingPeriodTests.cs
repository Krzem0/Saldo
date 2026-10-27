using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Saldo.Domain.Entities;
using Saldo.Domain.Enums;
using Saldo.Infrastructure.Sqlite.Persistence;
using Saldo.Infrastructure.Sqlite.Repositories;
using Saldo.Tests.Integration.Helpers;

namespace Saldo.Tests.Integration.Repositories;

public sealed class AccountingPeriodTests
{
    [Fact]
    public async Task ExistingMonthWrites_DoNotConsumePeriodIds()
    {
        using var db = new TestDatabase(useMigrations: true);
        var categoryId = await AddCategoryAsync(db.Context);
        var transactions = new TransactionRepository(db.Context);
        var periods = new AccountingPeriodRepository(db.Context);
        var transaction = MakeTransaction(categoryId, new DateOnly(2026, 1, 10));
        await transactions.AddAsync(transaction);
        var januaryId = await db.Context.AccountingPeriods.Select(period => period.Id).SingleAsync();

        await transactions.AddAsync(MakeTransaction(categoryId, new DateOnly(2026, 1, 20)));
        transaction.Amount = 123m;
        await transactions.UpdateAsync(transaction);
        var january = Saldo.Domain.ValueObjects.CalendarMonth.FromDate(transaction.Date);
        await periods.SetClosedAsync(january, true);
        await periods.SetClosedAsync(january, false);
        await periods.SetClosedAsync(january, true);
        await transactions.AddAsync(MakeTransaction(categoryId, new DateOnly(2026, 2, 1)));

        var saved = await db.Context.AccountingPeriods.AsNoTracking().OrderBy(period => period.StartDate).ToListAsync();
        Assert.Equal(new[] { januaryId, januaryId + 1 }, saved.Select(period => period.Id));
        Assert.True(saved[0].IsClosed);
        Assert.Equal(2, saved[0].ClosedTransactionCount);
    }

    [Fact]
    public async Task FreshDatabaseAndMonthReads_DoNotCreatePeriods()
    {
        using var db = new TestDatabase(useMigrations: true);
        var repository = new TransactionRepository(db.Context);

        Assert.False(db.Context.Database.HasPendingModelChanges());
        Assert.Empty(await db.Context.AccountingPeriods.ToListAsync());
        Assert.Empty(await repository.GetByMonthAsync(2026, 10));
        Assert.Empty(await repository.GetByMonthAsync(2023, 10));
        Assert.Empty(await db.Context.AccountingPeriods.ToListAsync());
    }

    [Fact]
    public async Task AddTransactions_CreatesOnlyOccupiedMonthsAndPreservesExistingPeriod()
    {
        using var db = new TestDatabase();
        var categoryId = await AddCategoryAsync(db.Context);
        var repository = new TransactionRepository(db.Context);
        await repository.AddAsync(MakeTransaction(categoryId, new DateOnly(2026, 1, 31)));

        var january = await db.Context.AccountingPeriods.SingleAsync();
        var januaryId = january.Id;
        Assert.False(january.IsClosed);
        Assert.Null(january.ClosedIncomeTotal);
        Assert.Null(january.ClosedExpenseTotal);
        Assert.Null(january.ClosedTransactionCount);
        Assert.Null(january.ClosedDataHash);
        Assert.Null(january.HashVersion);
        // A future closing snapshot must not be overwritten by period creation.
        january.ClosedIncomeTotal = 100m;
        january.ClosedExpenseTotal = 10m;
        january.ClosedTransactionCount = 1;
        january.ClosedDataHash = new string('a', 64);
        january.HashVersion = 1;
        await db.Context.SaveChangesAsync();

        await repository.AddAsync(MakeTransaction(categoryId, new DateOnly(2026, 1, 1)));
        await repository.AddAsync(MakeTransaction(categoryId, new DateOnly(2026, 4, 1)));

        var periods = await db.Context.AccountingPeriods.AsNoTracking().OrderBy(p => p.StartDate).ToListAsync();
        Assert.Equal(2, periods.Count);
        Assert.Equal(januaryId, periods[0].Id);
        Assert.Equal(new DateOnly(2026, 1, 1), periods[0].StartDate);
        Assert.Equal(new DateOnly(2026, 2, 1), periods[0].EndDateExclusive);
        Assert.Equal(100m, periods[0].ClosedIncomeTotal);
        Assert.Equal(10m, periods[0].ClosedExpenseTotal);
        Assert.Equal(1, periods[0].ClosedTransactionCount);
        Assert.Equal(new string('a', 64), periods[0].ClosedDataHash);
        Assert.Equal(1, periods[0].HashVersion);
        Assert.Equal(new DateOnly(2026, 4, 1), periods[1].StartDate);
        Assert.Equal(new DateOnly(2026, 5, 1), periods[1].EndDateExclusive);
    }

    [Fact]
    public async Task MoveAndDeleteLastTransaction_LeavesSourceAndDestinationPeriods()
    {
        using var db = new TestDatabase();
        var categoryId = await AddCategoryAsync(db.Context);
        var repository = new TransactionRepository(db.Context);
        var transaction = MakeTransaction(categoryId, new DateOnly(2026, 1, 10));
        await repository.AddAsync(transaction);

        transaction.Date = new DateOnly(2026, 4, 10);
        await repository.UpdateAsync(transaction);
        Assert.Empty(await repository.GetByMonthAsync(2026, 1));
        Assert.Single(await repository.GetByMonthAsync(2026, 4));
        await repository.DeleteAsync(transaction.Id);

        Assert.Empty(await db.Context.Transactions.AsNoTracking().ToListAsync());
        Assert.Equal(new[] { new DateOnly(2026, 1, 1), new DateOnly(2026, 4, 1) },
            await db.Context.AccountingPeriods.OrderBy(p => p.StartDate).Select(p => p.StartDate).ToArrayAsync());
    }

    [Fact]
    public async Task FailedAdd_RollsBackNewPeriod()
    {
        using var db = new TestDatabase();
        var repository = new TransactionRepository(db.Context);

        await Assert.ThrowsAsync<DbUpdateException>(() => repository.AddAsync(
            MakeTransaction(999, new DateOnly(2026, 4, 1))));

        Assert.Empty(await db.Context.AccountingPeriods.AsNoTracking().ToListAsync());
        Assert.Empty(await db.Context.Transactions.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task FailedMove_RollsBackDestinationPeriodAndTagChanges()
    {
        using var db = new TestDatabase();
        var categoryId = await AddCategoryAsync(db.Context);
        var tag = new Tag { Name = "Original" };
        db.Context.Tags.Add(tag);
        await db.Context.SaveChangesAsync();
        var repository = new TransactionRepository(db.Context);
        var transaction = MakeTransaction(categoryId, new DateOnly(2026, 1, 10));
        transaction.Tags = [new TransactionTag { TagId = tag.Id }];
        await repository.AddAsync(transaction);

        transaction.Date = new DateOnly(2026, 4, 10);
        transaction.Tags = [new TransactionTag { TagId = 999 }];
        await Assert.ThrowsAsync<DbUpdateException>(() => repository.UpdateAsync(transaction));
        db.Context.ChangeTracker.Clear();

        var saved = await repository.GetByIdAsync(transaction.Id);
        Assert.NotNull(saved);
        Assert.Equal(new DateOnly(2026, 1, 10), saved.Date);
        Assert.Equal(tag.Id, Assert.Single(saved.Tags).TagId);
        Assert.Single(await db.Context.AccountingPeriods.ToListAsync());
    }

    [Theory]
    [InlineData(2024, 2, 29)]
    [InlineData(2025, 2, 28)]
    [InlineData(2026, 12, 31)]
    public async Task MonthRead_IncludesLastDayAndExcludesNextMonth(int year, int month, int lastDay)
    {
        using var db = new TestDatabase();
        var categoryId = await AddCategoryAsync(db.Context);
        var repository = new TransactionRepository(db.Context);
        var first = new DateOnly(year, month, 1);
        await repository.AddAsync(MakeTransaction(categoryId, first.AddDays(-1)));
        await repository.AddAsync(MakeTransaction(categoryId, first));
        await repository.AddAsync(MakeTransaction(categoryId, new DateOnly(year, month, lastDay)));
        await repository.AddAsync(MakeTransaction(categoryId, first.AddMonths(1)));

        var result = await repository.GetByMonthAsync(year, month);

        Assert.Equal(new[] { new DateOnly(year, month, lastDay), first }, result.Select(t => t.Date));
    }

    [Theory]
    [InlineData("2026-01-01", "2026-01-01")]
    [InlineData("2026-01-02", "2026-02-01")]
    [InlineData("2026-01-01", "2026-03-01")]
    [InlineData("2026-01-01", "2025-12-01")]
    [InlineData("2026-02-30", "2026-03-01")]
    [InlineData("invalid", "2026-02-01")]
    [InlineData("9999-12-01", "10000-01-01")]
    public async Task Database_RejectsInvalidOrNonMonthlyBounds(string start, string end)
    {
        using var db = new TestDatabase(useMigrations: true);

        await Assert.ThrowsAsync<SqliteException>(() => db.Context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO AccountingPeriods (StartDate, EndDateExclusive, IsClosed)
            VALUES ({start}, {end}, 0);
            """));
    }

    [Fact]
    public async Task Database_RejectsDuplicateMonth()
    {
        using var db = new TestDatabase(useMigrations: true);
        await db.Context.Database.ExecuteSqlRawAsync("""
            INSERT INTO AccountingPeriods (StartDate, EndDateExclusive, IsClosed)
            VALUES ('2026-01-01', '2026-02-01', 0);
            """);

        await Assert.ThrowsAsync<SqliteException>(() => db.Context.Database.ExecuteSqlRawAsync("""
            INSERT INTO AccountingPeriods (StartDate, EndDateExclusive, IsClosed)
            VALUES ('2026-01-01', '2026-02-01', 0);
            """));
    }

    [Fact]
    public async Task Migration_BackfillsOnlyOccupiedMonthsAndPreservesTransactions()
    {
        using var db = new TestDatabase(useMigrations: true);
        var migrator = db.Context.GetService<IMigrator>();
        // Return the disposable test database to the schema that existed before this feature.
        await migrator.MigrateAsync("20260406194301_InitialSchema");
        var categoryId = await AddCategoryAsync(db.Context);
        db.Context.Transactions.AddRange(
            MakeTransaction(categoryId, new DateOnly(2024, 2, 1)),
            MakeTransaction(categoryId, new DateOnly(2024, 2, 29)),
            MakeTransaction(categoryId, new DateOnly(2024, 5, 31)));
        await db.Context.SaveChangesAsync();
        var originalIds = await db.Context.Transactions.OrderBy(t => t.Id).Select(t => t.Id).ToArrayAsync();
        db.Context.ChangeTracker.Clear();

        await migrator.MigrateAsync();
        await migrator.MigrateAsync(); // Reapplying migrations must not duplicate periods.

        var periods = await db.Context.AccountingPeriods.OrderBy(p => p.StartDate).ToListAsync();
        Assert.Equal(2, periods.Count);
        Assert.Equal(new DateOnly(2024, 2, 1), periods[0].StartDate);
        Assert.Equal(new DateOnly(2024, 3, 1), periods[0].EndDateExclusive);
        Assert.Equal(new DateOnly(2024, 5, 1), periods[1].StartDate);
        Assert.Equal(new DateOnly(2024, 6, 1), periods[1].EndDateExclusive);
        Assert.All(periods, p =>
        {
            Assert.False(p.IsClosed);
            Assert.Null(p.ClosedIncomeTotal);
            Assert.Null(p.ClosedExpenseTotal);
            Assert.Null(p.ClosedTransactionCount);
            Assert.Null(p.ClosedDataHash);
            Assert.Null(p.HashVersion);
        });
        Assert.Equal(originalIds, await db.Context.Transactions.OrderBy(t => t.Id).Select(t => t.Id).ToArrayAsync());
        Assert.Equal(30m, (await db.Context.Transactions.Select(t => t.Amount).ToListAsync()).Sum());
        Assert.False(db.Context.Database.HasPendingModelChanges());
    }

    private static async Task<int> AddCategoryAsync(SaldoDbContext context)
    {
        var category = new Category { Name = "Food" };
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        return category.Id;
    }

    private static Transaction MakeTransaction(int categoryId, DateOnly date) => new()
    {
        Date = date,
        Type = TransactionType.Expense,
        Amount = 10m,
        CategoryId = categoryId,
        Description = "Test"
    };
}
