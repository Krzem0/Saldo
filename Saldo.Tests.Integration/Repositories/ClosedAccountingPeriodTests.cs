using Microsoft.EntityFrameworkCore;
using Saldo.Application.Errors;
using Saldo.Application.UseCases;
using Saldo.Domain.Entities;
using Saldo.Domain.Enums;
using Saldo.Infrastructure.Sqlite.Persistence;
using Saldo.Infrastructure.Sqlite.Repositories;
using Saldo.Tests.Integration.Helpers;

namespace Saldo.Tests.Integration.Repositories;

public sealed class ClosedAccountingPeriodTests
{
    [Fact]
    public async Task CloseEmptyMonth_IsIdempotentAndDoesNotFillGaps()
    {
        using var db = new TestDatabase(useMigrations: true);
        var periods = new AccountingPeriodRepository(db.Context);
        var close = new CloseAccountingPeriod(periods);

        Assert.True((await close.ExecuteAsync(2026, 1)).IsSuccess);
        Assert.True((await close.ExecuteAsync(2026, 4)).IsSuccess);
        var original = await periods.GetByMonthAsync(2026, 1);
        Assert.True((await close.ExecuteAsync(2026, 1)).IsSuccess);

        var january = await periods.GetByMonthAsync(2026, 1);
        Assert.NotNull(january);
        Assert.True(january.IsClosed);
        Assert.Equal(original!.Id, january.Id);
        Assert.Null(january.ClosedIncomeTotal);
        Assert.Null(january.ClosedExpenseTotal);
        Assert.Null(january.ClosedTransactionCount);
        Assert.Null(january.ClosedDataHash);
        Assert.Null(january.HashVersion);
        Assert.Null(await periods.GetByMonthAsync(2026, 2));
        Assert.Equal(2, await db.Context.AccountingPeriods.CountAsync());
    }

    [Fact]
    public async Task ReopenAbsentMonth_DoesNotCreatePeriod()
    {
        using var db = new TestDatabase();
        var periods = new AccountingPeriodRepository(db.Context);

        Assert.True((await new ReopenAccountingPeriod(periods).ExecuteAsync(2026, 1)).IsSuccess);

        Assert.Null(await periods.GetByMonthAsync(2026, 1));
        Assert.Empty(await db.Context.AccountingPeriods.ToListAsync());
    }

    [Fact]
    public async Task ReopenAndRepeatedClose_PreserveExistingSnapshot()
    {
        using var db = new TestDatabase();
        var period = new AccountingPeriod
        {
            StartDate = new DateOnly(2026, 1, 1), EndDateExclusive = new DateOnly(2026, 2, 1), IsClosed = true,
            ClosedIncomeTotal = 100m, ClosedExpenseTotal = 25m, ClosedTransactionCount = 2,
            ClosedDataHash = new string('a', 64), HashVersion = 1
        };
        db.Context.AccountingPeriods.Add(period);
        await db.Context.SaveChangesAsync();
        var periods = new AccountingPeriodRepository(db.Context);

        await new ReopenAccountingPeriod(periods).ExecuteAsync(2026, 1);
        Assert.False((await periods.GetByMonthAsync(2026, 1))!.IsClosed);
        await new CloseAccountingPeriod(periods).ExecuteAsync(2026, 1);
        var saved = await periods.GetByMonthAsync(2026, 1);

        Assert.True(saved!.IsClosed);
        Assert.Equal(100m, saved.ClosedIncomeTotal);
        Assert.Equal(25m, saved.ClosedExpenseTotal);
        Assert.Equal(2, saved.ClosedTransactionCount);
        Assert.Equal(new string('a', 64), saved.ClosedDataHash);
        Assert.Equal(1, saved.HashVersion);
    }

    [Fact]
    public async Task DirectAdd_RejectsClosedMonthButAllowsNextMonthsBoundaryAfterReopen()
    {
        using var db = new TestDatabase();
        var category = await AddCategoryAsync(db.Context);
        var periods = new AccountingPeriodRepository(db.Context);
        var transactions = new TransactionRepository(db.Context);
        await new CloseAccountingPeriod(periods).ExecuteAsync(2026, 1);

        await Assert.ThrowsAsync<AccountingPeriodClosedException>(() =>
            transactions.AddAsync(NewTransaction(category.Id, new DateOnly(2026, 1, 31))));
        Assert.Empty(await db.Context.Transactions.ToListAsync());

        await transactions.AddAsync(NewTransaction(category.Id, new DateOnly(2026, 2, 1)));
        await new ReopenAccountingPeriod(periods).ExecuteAsync(2026, 1);
        await transactions.AddAsync(NewTransaction(category.Id, new DateOnly(2026, 1, 31)));
        Assert.Equal(2, await db.Context.Transactions.CountAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DirectEdit_RejectsClosedSourceOrDestinationWithoutChangingTagsOrCreatingPeriods(bool closeSource)
    {
        using var db = new TestDatabase();
        var category = await AddCategoryAsync(db.Context);
        var tag = new Tag { Name = "Original" };
        db.Context.Tags.Add(tag);
        await db.Context.SaveChangesAsync();
        var transactions = new TransactionRepository(db.Context);
        var transaction = NewTransaction(category.Id, new DateOnly(2026, 1, 10));
        transaction.Tags = [new TransactionTag { TagId = tag.Id }];
        await transactions.AddAsync(transaction);
        var periods = new AccountingPeriodRepository(db.Context);
        await new CloseAccountingPeriod(periods).ExecuteAsync(2026, closeSource ? 1 : 4);

        // Even if the caller has already changed the tracked entity's date, the
        // guard must use the database's original date to protect the source.
        transaction.Date = new DateOnly(2026, 4, 10);
        transaction.Description = "Changed";
        transaction.Tags = [];
        await Assert.ThrowsAsync<AccountingPeriodClosedException>(() => transactions.UpdateAsync(transaction));
        db.Context.ChangeTracker.Clear();

        var saved = await transactions.GetByIdAsync(transaction.Id);
        Assert.Equal(new DateOnly(2026, 1, 10), saved!.Date);
        Assert.Equal("Original", saved.Description);
        Assert.Equal(tag.Id, Assert.Single(saved.Tags).TagId);
        Assert.Equal(closeSource ? 1 : 2, await db.Context.AccountingPeriods.CountAsync());
    }

    [Fact]
    public async Task DirectTagOnlyEditAndDelete_AreBlockedUntilReopen()
    {
        using var db = new TestDatabase();
        var category = await AddCategoryAsync(db.Context);
        var transactions = new TransactionRepository(db.Context);
        var transaction = NewTransaction(category.Id, new DateOnly(2026, 1, 10));
        await transactions.AddAsync(transaction);
        var periods = new AccountingPeriodRepository(db.Context);
        await new CloseAccountingPeriod(periods).ExecuteAsync(2026, 1);

        transaction.Tags = [new TransactionTag { TagId = 999 }];
        await Assert.ThrowsAsync<AccountingPeriodClosedException>(() => transactions.UpdateAsync(transaction));
        await Assert.ThrowsAsync<AccountingPeriodClosedException>(() => transactions.DeleteAsync(transaction.Id));
        Assert.Single(await transactions.GetByMonthAsync(2026, 1));
        Assert.Empty(await db.Context.TransactionTags.AsNoTracking().ToListAsync());

        await new ReopenAccountingPeriod(periods).ExecuteAsync(2026, 1);
        await transactions.DeleteAsync(transaction.Id);
        Assert.Empty(await transactions.GetByMonthAsync(2026, 1));
        Assert.False((await periods.GetByMonthAsync(2026, 1))!.IsClosed);
    }

    [Fact]
    public async Task CloseFromAnotherConnection_RejectsStaleEditAndDelete()
    {
        using var db = new TestDatabase();
        var category = await AddCategoryAsync(db.Context);
        var transactions = new TransactionRepository(db.Context);
        var transaction = NewTransaction(category.Id, new DateOnly(2026, 1, 10));
        await transactions.AddAsync(transaction);
        var draft = (await transactions.GetByIdAsync(transaction.Id))!;
        await using var other = OpenContext(db.Context);
        await new CloseAccountingPeriod(new AccountingPeriodRepository(other)).ExecuteAsync(2026, 1);

        draft.Amount = 99m;
        await Assert.ThrowsAsync<AccountingPeriodClosedException>(() => transactions.UpdateAsync(draft));
        await Assert.ThrowsAsync<AccountingPeriodClosedException>(() => transactions.DeleteAsync(draft.Id));

        Assert.Equal(10m, (await transactions.GetByIdAsync(draft.Id))!.Amount);
    }

    private static SaldoDbContext OpenContext(SaldoDbContext source) => new(
        new DbContextOptionsBuilder<SaldoDbContext>().UseSqlite(source.Database.GetConnectionString()!).Options);

    private static async Task<Category> AddCategoryAsync(SaldoDbContext context)
    {
        var category = new Category { Name = "Food" };
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        return category;
    }

    private static Transaction NewTransaction(int categoryId, DateOnly date) => new()
    {
        Date = date, Type = TransactionType.Expense, Amount = 10m,
        CategoryId = categoryId, Description = "Original"
    };
}
