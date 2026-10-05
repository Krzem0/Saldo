using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Saldo.Domain.Entities;
using Saldo.Domain.Enums;
using Saldo.Infrastructure.Sqlite.Persistence;
using Saldo.Tests.Integration.Helpers;

namespace Saldo.Tests.Integration.Backup;

public sealed class SqliteDatabaseBackupServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"saldo-backup-test-{Guid.NewGuid():N}");
    private string BackupPath => Path.Combine(_directory, "backup.db");

    public SqliteDatabaseBackupServiceTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task CreateAsync_OpenWalDatabase_CreatesStandaloneSnapshotWithAllData()
    {
        using var db = new TestDatabase(useMigrations: true);
        await db.Context.Database.OpenConnectionAsync();
        await db.Context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
        await db.Context.Database.ExecuteSqlRawAsync("PRAGMA wal_autocheckpoint=0;");
        var category = new Category { Name = "Food", ColorCode = "#112233" };
        var party = new Party { Name = "Me" };
        var location = new Location { Name = "Shop" };
        var tag = new Tag { Name = "groceries" };
        var transaction = new Transaction
        {
            Date = new DateOnly(2026, 10, 4), Amount = 42.50m, Type = TransactionType.Expense,
            Category = category, Payer = party, Counterparty = party, Location = location,
            Tags = [new TransactionTag { Tag = tag }]
        };
        db.Context.Transactions.Add(transaction);
        await db.Context.SaveChangesAsync();
        var sourcePath = new SqliteConnectionStringBuilder(db.Context.Database.GetConnectionString()).DataSource;
        Assert.True(new FileInfo(sourcePath + "-wal").Length > 0);

        await new SqliteDatabaseBackupService(db.Context).CreateAsync(BackupPath);

        // Later changes to the live database must not alter the saved snapshot.
        category.Name = "Changed after backup";
        await db.Context.SaveChangesAsync();
        using var backup = OpenBackup();
        var saved = await backup.Transactions.Include(t => t.Category).Include(t => t.Payer)
            .Include(t => t.Counterparty).Include(t => t.Location).Include(t => t.Tags).ThenInclude(t => t.Tag)
            .SingleAsync();
        Assert.Equal(42.50m, saved.Amount);
        Assert.Equal("Food", saved.Category.Name);
        Assert.Equal("#112233", saved.Category.ColorCode);
        Assert.Equal("Me", Assert.IsType<Party>(saved.Payer).Name);
        Assert.Equal("Me", Assert.IsType<Party>(saved.Counterparty).Name);
        Assert.Equal("Shop", saved.Location!.Name);
        Assert.Equal("groceries", Assert.Single(saved.Tags).Tag.Name);
        Assert.Equal(await db.Context.Database.GetAppliedMigrationsAsync(), await backup.Database.GetAppliedMigrationsAsync());
        await backup.Database.OpenConnectionAsync();
        using var integrityCheck = backup.Database.GetDbConnection().CreateCommand();
        integrityCheck.CommandText = "PRAGMA integrity_check;";
        Assert.Equal("ok", await integrityCheck.ExecuteScalarAsync());
        Assert.Single(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task CreateAsync_ExistingBackup_ReplacesItWithCompletedSnapshot()
    {
        using var db = new TestDatabase();
        db.Context.Categories.Add(new Category { Name = "Food" });
        await db.Context.SaveChangesAsync();
        await File.WriteAllTextAsync(BackupPath, "previous backup");

        await new SqliteDatabaseBackupService(db.Context).CreateAsync(BackupPath);

        using var backup = OpenBackup();
        Assert.Equal("Food", (await backup.Categories.SingleAsync()).Name);
        Assert.Single(Directory.GetFiles(_directory));
    }

    [Theory]
    [InlineData("")]
    [InlineData("-wal")]
    [InlineData("-shm")]
    [InlineData("-journal")]
    public async Task CreateAsync_LiveDatabaseOrSidecarDestination_RejectsWithoutChangingData(string suffix)
    {
        using var db = new TestDatabase();
        db.Context.Categories.Add(new Category { Name = "Food" });
        await db.Context.SaveChangesAsync();
        var sourcePath = new SqliteConnectionStringBuilder(db.Context.Database.GetConnectionString()).DataSource;

        var error = await Assert.ThrowsAsync<ArgumentException>(
            () => new SqliteDatabaseBackupService(db.Context).CreateAsync(sourcePath + suffix));

        Assert.Equal("destinationPath", error.ParamName);
        db.Context.ChangeTracker.Clear();
        Assert.Equal("Food", (await db.Context.Categories.SingleAsync()).Name);
    }

    [Fact]
    public async Task CreateAsync_SourceMissing_PreservesExistingBackupAndCleansTemporaryFiles()
    {
        using var source = new SaldoDbContext(new DbContextOptionsBuilder<SaldoDbContext>()
            .UseSqlite($"Data Source={Path.Combine(_directory, "missing.db")}").Options);
        await File.WriteAllTextAsync(BackupPath, "previous backup");

        await Assert.ThrowsAsync<SqliteException>(
            () => new SqliteDatabaseBackupService(source).CreateAsync(BackupPath));

        Assert.Equal("previous backup", await File.ReadAllTextAsync(BackupPath));
        Assert.Single(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task CreateAsync_Cancelled_PreservesExistingBackup()
    {
        using var db = new TestDatabase();
        await File.WriteAllTextAsync(BackupPath, "previous backup");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new SqliteDatabaseBackupService(db.Context).CreateAsync(BackupPath, cancellation.Token));

        Assert.Equal("previous backup", await File.ReadAllTextAsync(BackupPath));
        Assert.Single(Directory.GetFiles(_directory));
    }

    private SaldoDbContext OpenBackup() => new(new DbContextOptionsBuilder<SaldoDbContext>()
        .UseSqlite(new SqliteConnectionStringBuilder
        {
            DataSource = BackupPath, Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString()).Options);
}
