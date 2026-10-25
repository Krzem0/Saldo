using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Saldo.Application.DTOs;
using Saldo.Application.Errors;
using Saldo.Application.Interfaces;
using Saldo.Application.UseCases;
using Saldo.Domain.Entities;
using Saldo.Domain.Enums;
using Saldo.Infrastructure.Sqlite.Persistence;
using Saldo.Infrastructure.Sqlite.Repositories;
using Xunit.Abstractions;

namespace Saldo.Tests.Integration.Backup;

public sealed class PeriodWorkflowVerificationTests(ITestOutputHelper output) : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "saldo-workflow-test-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task MigratedDatabase_StartupTransactionsClosingBackupReopeningAndReclosing_WorkTogether()
    {
        Directory.CreateDirectory(_directory);
        var options = new DbContextOptionsBuilder<SaldoDbContext>().UseSqlite($"Data Source={Path.Combine(_directory, "workflow.db")}").Options;
        using (var bootstrap = new SaldoDbContext(options))
        {
            await bootstrap.Database.MigrateAsync();
            bootstrap.Categories.Add(new Category { Name = "General" });
            await bootstrap.SaveChangesAsync();
        }
        var access = new Access();
        using var context = new SaldoDbContext(options, access);
        var transactions = new TransactionRepository(context);
        var periods = new AccountingPeriodRepository(context);
        var parties = new PartyRepository(context, NullLogger<PartyRepository>.Instance);
        var locations = new LocationRepository(context, NullLogger<LocationRepository>.Instance);
        var add = new AddTransaction(transactions, parties, locations, periods);
        var edit = new EditTransaction(transactions, parties, locations, periods);
        var close = new CloseAccountingPeriod(periods);
        var reopen = new ReopenAccountingPeriod(periods);
        var verify = new VerifyAccountingPeriod(periods);
        var backups = new AutomaticDatabaseBackupService(context.Database.GetConnectionString()!);
        var date = new DateOnly(2026, 1, 10);
        AddTransactionCommand Command(TransactionType type, decimal amount) => new(date, type, amount, 1,
            null, null, null, null, "Transaction", null, []);
        await Assert.ThrowsAsync<DatabaseWriteBlockedException>(() => add.ExecuteAsync(Command(TransactionType.Expense, 12.34m)));

        var morning = await backups.EnsureDailyAsync();
        access.CanWrite = true;
        var expense = (await add.ExecuteAsync(Command(TransactionType.Expense, 12.34m))).Value;
        Assert.True((await add.ExecuteAsync(Command(TransactionType.Income, 100m))).IsSuccess);
        Assert.True((await close.ExecuteAsync(2026, 1)).IsSuccess);
        var firstSnapshot = (await periods.GetByMonthAsync(2026, 1))!.ClosedDataHash;
        var evening = await backups.CreateAfterClosingAsync(2026, 1);
        Assert.Equal(morning.Path, evening.Path);
        var savedBytes = await File.ReadAllBytesAsync(evening.Path);
        var editCommand = new EditTransactionCommand(expense.Id, date, TransactionType.Expense, 20m, 1,
            null, null, null, null, "Approved correction", null, []);
        Assert.Equal(ErrorCodes.AccountingPeriod.Closed, Assert.Single((await edit.ExecuteAsync(editCommand)).Errors).Message);
        Assert.True((await new DeleteTransaction(transactions, periods).ExecuteAsync(expense.Id)).IsFailed);
        Assert.True((await add.ExecuteAsync(Command(TransactionType.Expense, 1m))).IsFailed);
        Assert.Equal(AccountingPeriodVerificationStatus.Valid, (await verify.ExecuteAsync(2026, 1)).Value.Status);

        Assert.True((await reopen.ExecuteAsync(2026, 1)).IsSuccess);
        Assert.Equal(firstSnapshot, (await periods.GetByMonthAsync(2026, 1))!.ClosedDataHash);
        Assert.True((await edit.ExecuteAsync(editCommand)).IsSuccess);
        Assert.Equal(AccountingPeriodVerificationStatus.Open, (await verify.ExecuteAsync(2026, 1)).Value.Status);
        Assert.Equal(savedBytes, await File.ReadAllBytesAsync(evening.Path));
        Assert.True((await close.ExecuteAsync(2026, 1)).IsSuccess);
        await backups.CreateAfterClosingAsync(2026, 1);

        using var copy = new SaldoDbContext(new DbContextOptionsBuilder<SaldoDbContext>().UseSqlite(
            new SqliteConnectionStringBuilder { DataSource = evening.Path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()).Options);
        var result = (await new VerifyAccountingPeriod(new AccountingPeriodRepository(copy)).ExecuteAsync(2026, 1)).Value;
        Assert.Equal(AccountingPeriodVerificationStatus.Valid, result.Status);
        Assert.Equal(100m, result.StoredSnapshot!.IncomeTotal);
        Assert.Equal(20m, result.StoredSnapshot.ExpenseTotal);
        Assert.Equal(80m, result.StoredSnapshot.Balance);
        Assert.Equal(2, result.StoredSnapshot.TransactionCount);
        Assert.NotEqual(firstSnapshot, result.StoredSnapshot.DataHash);
        Assert.Single(Directory.GetFiles(Path.Combine(_directory, "AutoBackups")));
        Assert.False(context.Database.HasPendingModelChanges());
        output.WriteLine("Full workflow passed on a migrated temporary SQLite database; final copied month verified successfully.");
    }

    [Fact]
    public async Task Verification_HandlesLargeMonthWithTags()
    {
        Directory.CreateDirectory(_directory);
        using var context = new SaldoDbContext(new DbContextOptionsBuilder<SaldoDbContext>()
            .UseSqlite($"Data Source={Path.Combine(_directory, "performance.db")}").Options);
        await context.Database.EnsureCreatedAsync();
        var category = new Category { Name = "General" };
        var tags = new[] { new Tag { Name = "A" }, new Tag { Name = "B" } };
        context.Add(category);
        context.AddRange(tags);
        await context.SaveChangesAsync();
        for (var i = 0; i < 5000; i++)
            context.Transactions.Add(new Transaction
            {
                Date = new DateOnly(2026, 1, i % 31 + 1), Type = TransactionType.Expense, Amount = 1.23m,
                CategoryId = category.Id, Description = $"Transaction {i}",
                Tags = tags.Select(tag => new TransactionTag { TagId = tag.Id }).ToList()
            });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var periods = new AccountingPeriodRepository(context);
        var timer = Stopwatch.StartNew();
        await new CloseAccountingPeriod(periods).ExecuteAsync(2026, 1);
        var closingMs = timer.Elapsed.TotalMilliseconds;
        timer.Restart();
        var result = await new VerifyAccountingPeriod(periods).ExecuteAsync(2026, 1);
        var verificationMs = timer.Elapsed.TotalMilliseconds;
        Assert.Equal(AccountingPeriodVerificationStatus.Valid, result.Value.Status);
        Assert.Equal(5000, result.Value.ActualSnapshot!.TransactionCount);
        Assert.Equal(6150m, result.Value.ActualSnapshot.ExpenseTotal);
        output.WriteLine($"5000 transactions / 10000 tag links: close {closingMs:F1} ms; verify {verificationMs:F1} ms. Debug build, local temporary SQLite file; no timing threshold.");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private sealed class Access : IDatabaseWriteAccess
    {
        public bool CanWrite { get; set; }
        public void EnsureCanWrite() { if (!CanWrite) throw new DatabaseWriteBlockedException(); }
    }
}
