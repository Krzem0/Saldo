using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Saldo.Application.Errors;
using Saldo.Application.Interfaces;
using Saldo.Application.UseCases;
using Saldo.Domain.Entities;
using Saldo.Domain.Enums;
using Saldo.Infrastructure.Sqlite.Persistence;
using Saldo.Infrastructure.Sqlite.Repositories;

namespace Saldo.Tests.Integration.Backup;

public sealed class AutomaticDatabaseBackupTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "saldo-auto-test-" + Guid.NewGuid().ToString("N"));
    private readonly MutableClock _clock = new();
    private readonly SaldoDbContext _context;
    private string ConnectionString => _context.Database.GetConnectionString()!;
    private string BackupDirectory => Path.Combine(_directory, "AutoBackups");
    private AutomaticDatabaseBackupService Service() => new(ConnectionString, _clock);

    public AutomaticDatabaseBackupTests()
    {
        Directory.CreateDirectory(_directory);
        _context = new SaldoDbContext(new DbContextOptionsBuilder<SaldoDbContext>()
            .UseSqlite(PathConnection("main.db")).Options);
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task Startup_CreatesOneValidDailyCopy_AndRestartDoesNotReplaceIt()
    {
        var first = await Service().EnsureDailyAsync();
        var original = await File.ReadAllBytesAsync(first.Path);
        _context.Categories.Add(new Category { Name = "Added after startup" });
        await _context.SaveChangesAsync();

        var restarted = await Service().EnsureDailyAsync();

        Assert.True(first.Created);
        Assert.False(restarted.Created);
        Assert.True(first.RetentionSucceeded);
        Assert.Equal(first.Path, restarted.Path);
        Assert.Equal(Path.Combine(BackupDirectory, "main.db.auto-2026-10-25.db"), first.Path);
        Assert.Equal(original, await File.ReadAllBytesAsync(first.Path));
        using var backup = ReadBackup(first.Path);
        Assert.Empty(await backup.Categories.ToListAsync());
    }

    [Fact]
    public async Task Retention_KeepsFiveSuccessfulDaysDespiteGaps_AndLeavesOtherFilesAlone()
    {
        var service = Service();
        var first = await service.EnsureDailyAsync();
        var manual = Path.Combine(BackupDirectory, "manual.db");
        var other = Path.Combine(BackupDirectory, "other.db.auto-2020-01-01.db");
        await File.WriteAllTextAsync(manual, "manual");
        await File.WriteAllTextAsync(other, "other");
        await File.WriteAllTextAsync(Path.Combine(BackupDirectory, "main.db.auto-invalid.db"), "unrelated");
        for (var i = 0; i < 6; i++)
        {
            _clock.UtcNow = _clock.UtcNow.AddDays(10);
            await service.EnsureDailyAsync();
        }

        Assert.Equal(5, Directory.GetFiles(BackupDirectory, "main.db.auto-????-??-??.db").Length);
        Assert.False(File.Exists(first.Path));
        Assert.Equal("manual", await File.ReadAllTextAsync(manual));
        Assert.Equal("other", await File.ReadAllTextAsync(other));
        Assert.True(File.Exists(Path.Combine(BackupDirectory, "main.db.auto-invalid.db")));
    }

    [Fact]
    public async Task ConcurrentStartupRequestsAcrossInstances_CreateOnlyOneCopy()
    {
        var results = await Task.WhenAll(Service().EnsureDailyAsync(), Service().EnsureDailyAsync(), Service().EnsureDailyAsync());
        Assert.Single(results, result => result.Created);
        Assert.Single(Directory.GetFiles(BackupDirectory));
    }

    [Fact]
    public async Task Closing_ReplacesTodaysCopyAndIncludesClosingSnapshot()
    {
        await SeedTransactionAsync();
        var service = Service();
        var first = await service.EnsureDailyAsync();
        using (var before = ReadBackup(first.Path))
            Assert.False((await before.AccountingPeriods.SingleAsync()).IsClosed);
        await new CloseAccountingPeriod(new AccountingPeriodRepository(_context)).ExecuteAsync(2026, 1);

        var after = await service.CreateAfterClosingAsync(2026, 1);

        Assert.True(after.Created);
        Assert.Equal(first.Path, after.Path);
        Assert.Single(Directory.GetFiles(BackupDirectory));
        using var backup = ReadBackup(after.Path);
        var period = await backup.AccountingPeriods.SingleAsync();
        Assert.True(period.IsClosed);
        Assert.Equal(10m, period.ClosedExpenseTotal);
        Assert.Equal(1, period.ClosedTransactionCount);
        Assert.Equal(64, period.ClosedDataHash!.Length);
    }

    [Fact]
    public async Task InconsistentClosedMonth_DoesNotReplaceDailyCopy()
    {
        await SeedTransactionAsync();
        var service = Service();
        var first = await service.EnsureDailyAsync();
        var original = await File.ReadAllBytesAsync(first.Path);
        await new CloseAccountingPeriod(new AccountingPeriodRepository(_context)).ExecuteAsync(2026, 1);
        await _context.Database.ExecuteSqlRawAsync("UPDATE Transactions SET Description = 'External change'");

        await Assert.ThrowsAsync<InvalidDataException>(() => service.CreateAfterClosingAsync(2026, 1));

        Assert.Equal(original, await File.ReadAllBytesAsync(first.Path));
        Assert.True((await _context.AccountingPeriods.AsNoTracking().SingleAsync()).IsClosed);
    }

    [Theory]
    [InlineData("UPDATE AccountingPeriods SET IsClosed = 0")]
    [InlineData("UPDATE Transactions SET Description = 'Changed after precheck'")]
    public async Task ChangesAfterSourcePrecheck_AreRejectedBeforeReplacingDailyCopy(string sql)
    {
        await SeedTransactionAsync();
        var service = Service();
        var first = await service.EnsureDailyAsync();
        var original = await File.ReadAllBytesAsync(first.Path);
        await new CloseAccountingPeriod(new AccountingPeriodRepository(_context)).ExecuteAsync(2026, 1);
        // The local-date lookup occurs after the source precheck and before the
        // copy. Simulate a second writer reopening/changing that month then.
        _clock.BeforeRead = () =>
        {
            _clock.BeforeRead = null;
            using var writer = new SqliteConnection(ConnectionString);
            writer.Open();
            using var command = writer.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        };

        await Assert.ThrowsAsync<InvalidDataException>(() => service.CreateAfterClosingAsync(2026, 1));

        Assert.Equal(original, await File.ReadAllBytesAsync(first.Path));
        Assert.Empty(Directory.GetFiles(BackupDirectory, ".saldo-backup-*"));
    }

    [Fact]
    public async Task FailedReplacement_PreservesPreviousCopyAndDoesNotPrune()
    {
        await SeedTransactionAsync();
        var service = Service();
        var first = await service.EnsureDailyAsync();
        var original = await File.ReadAllBytesAsync(first.Path);
        for (var day = 1; day <= 5; day++)
            await File.WriteAllTextAsync(Path.Combine(BackupDirectory, $"main.db.auto-2020-01-0{day}.db"), "old");
        await new CloseAccountingPeriod(new AccountingPeriodRepository(_context)).ExecuteAsync(2026, 1);
        using (var locked = new FileStream(first.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var error = await Record.ExceptionAsync(() => service.CreateAfterClosingAsync(2026, 1));
            Assert.True(error is IOException or UnauthorizedAccessException);
        }

        Assert.Equal(original, await File.ReadAllBytesAsync(first.Path));
        Assert.Equal(6, Directory.GetFiles(BackupDirectory).Length);
        Assert.Empty(Directory.GetFiles(BackupDirectory, ".saldo-backup-*"));
    }

    [Fact]
    public async Task CorruptedExistingDailyCopy_IsReplaced()
    {
        var service = Service();
        var first = await service.EnsureDailyAsync();
        await File.WriteAllTextAsync(first.Path, "invalid SQLite file");

        var result = await service.EnsureDailyAsync();

        Assert.True(result.Created);
        using var backup = ReadBackup(result.Path);
        Assert.Empty(await backup.Transactions.ToListAsync());
    }

    [Fact]
    public async Task RetentionFailure_KeepsNewCopy_AndRetryOnlyPrunes()
    {
        var service = Service();
        var first = await service.EnsureDailyAsync();
        for (var day = 1; day <= 5; day++)
            await File.WriteAllTextAsync(Path.Combine(BackupDirectory, $"main.db.auto-2020-01-0{day}.db"), "old");
        var oldest = Path.Combine(BackupDirectory, "main.db.auto-2020-01-01.db");
        using (var locked = new FileStream(oldest, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var result = await service.EnsureDailyAsync();
            Assert.False(result.Created);
            Assert.False(result.RetentionSucceeded);
            Assert.True(File.Exists(first.Path));
        }
        var original = await File.ReadAllBytesAsync(first.Path);
        var retry = await service.EnsureDailyAsync();
        Assert.False(retry.Created);
        Assert.True(retry.RetentionSucceeded);
        Assert.Equal(5, Directory.GetFiles(BackupDirectory).Length);
        Assert.Equal(original, await File.ReadAllBytesAsync(first.Path));
    }

    [Fact]
    public async Task UnrelatedSqliteFile_DoesNotCountAsSuccessfulDailyBackup()
    {
        var service = Service();
        var first = await service.EnsureDailyAsync();
        File.Delete(first.Path);
        using (var unrelated = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = first.Path, Pooling = false }.ToString()))
        {
            await unrelated.OpenAsync();
            using var command = unrelated.CreateCommand();
            command.CommandText = "CREATE TABLE Unrelated (Id INTEGER);";
            await command.ExecuteNonQueryAsync();
        }
        var result = await service.EnsureDailyAsync();
        Assert.True(result.Created);
        using var backup = ReadBackup(result.Path);
        Assert.Empty(await backup.Transactions.ToListAsync());
    }

    [Fact]
    public async Task SameFolderDifferentDatabases_HaveIndependentNamesAndRetention()
    {
        var first = await Service().EnsureDailyAsync();
        var secondConnection = PathConnection("second.db");
        using var second = new SaldoDbContext(new DbContextOptionsBuilder<SaldoDbContext>().UseSqlite(secondConnection).Options);
        await second.Database.EnsureCreatedAsync();
        var otherService = new AutomaticDatabaseBackupService(secondConnection, _clock);
        var other = await otherService.EnsureDailyAsync();
        for (var i = 0; i < 6; i++)
        {
            _clock.UtcNow = _clock.UtcNow.AddDays(1);
            await otherService.EnsureDailyAsync();
        }
        Assert.True(File.Exists(first.Path));
        Assert.NotEqual(first.Path, other.Path);
        Assert.Equal(5, Directory.GetFiles(BackupDirectory, "second.db.auto-*.db").Length);
    }

    [Fact]
    public async Task LocalDay_IsUsedInsteadOfUtcDay()
    {
        _clock.UtcNow = new DateTimeOffset(2026, 10, 24, 23, 30, 0, TimeSpan.Zero);
        var result = await Service().EnsureDailyAsync();
        Assert.Equal(new DateOnly(2026, 10, 25), result.Date);
    }

    [Fact]
    public async Task StartupWriteGate_BlocksSavesAndBulkDeletesButAllowsReads()
    {
        await SeedTransactionAsync();
        var access = new WriteAccess();
        using var guarded = new SaldoDbContext(new DbContextOptionsBuilder<SaldoDbContext>().UseSqlite(ConnectionString).Options, access);
        var transactions = new TransactionRepository(guarded);
        Assert.Single(await transactions.GetByMonthAsync(2026, 1));
        await Assert.ThrowsAsync<DatabaseWriteBlockedException>(() => transactions.DeleteAsync(1));
        await Assert.ThrowsAsync<DatabaseWriteBlockedException>(() => new AccountingPeriodRepository(guarded)
            .SetClosedAsync(Saldo.Domain.ValueObjects.CalendarMonth.FromYearMonth(2026, 1), true));
        guarded.Categories.Add(new Category { Name = "Blocked" });
        await Assert.ThrowsAsync<DatabaseWriteBlockedException>(() => guarded.SaveChangesAsync());
        Assert.Single(await transactions.GetByMonthAsync(2026, 1));
        access.CanWrite = true;
        await guarded.SaveChangesAsync();
        await transactions.DeleteAsync(1);
        Assert.Empty(await transactions.GetByMonthAsync(2026, 1));
    }

    private async Task SeedTransactionAsync()
    {
        var category = new Category { Name = "Food" };
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();
        await new TransactionRepository(_context).AddAsync(new Transaction
        {
            Date = new DateOnly(2026, 1, 10), Amount = 10m, Type = TransactionType.Expense,
            CategoryId = category.Id, Description = "Original"
        });
    }

    private string PathConnection(string name) => new SqliteConnectionStringBuilder { DataSource = Path.Combine(_directory, name) }.ToString();
    private static SaldoDbContext ReadBackup(string path) => new(new DbContextOptionsBuilder<SaldoDbContext>()
        .UseSqlite(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()).Options);
    public void Dispose()
    {
        _context.Dispose();
        SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
    }

    private sealed class MutableClock : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 25, 10, 0, 0, TimeSpan.Zero);
        public Action? BeforeRead { get; set; }
        public override DateTimeOffset GetUtcNow() { BeforeRead?.Invoke(); return UtcNow; }
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.CreateCustomTimeZone("Test", TimeSpan.FromHours(2), "Test", "Test");
    }

    private sealed class WriteAccess : IDatabaseWriteAccess
    {
        public bool CanWrite { get; set; }
        public void EnsureCanWrite() { if (!CanWrite) throw new DatabaseWriteBlockedException(); }
    }
}
