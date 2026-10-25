using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Saldo.Application.Interfaces;
using System.Collections.Concurrent;

namespace Saldo.Infrastructure.Sqlite.Persistence;

public sealed class SqliteDatabaseBackupService : IDatabaseBackupService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> SourceLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _connectionString;

    public SqliteDatabaseBackupService(SaldoDbContext context) : this(context.Database.GetConnectionString()!) { }
    internal SqliteDatabaseBackupService(string connectionString) => _connectionString = connectionString;

    public Task CreateAsync(string destinationPath, CancellationToken ct = default) => CreateValidatedAsync(destinationPath, null, ct);

    internal async Task CreateValidatedAsync(string destinationPath, Func<string, CancellationToken, Task>? validate, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        var source = new SqliteConnectionStringBuilder(_connectionString);
        var sourcePath = Path.GetFullPath(source.DataSource);
        var targetPath = Path.GetFullPath(destinationPath);

        // Never replace the live database or any of SQLite's live sidecar files.
        if (new[] { sourcePath, sourcePath + "-wal", sourcePath + "-shm", sourcePath + "-journal" }
            .Any(path => string.Equals(path, targetPath, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("Choose a backup file different from the active database and its sidecar files.", nameof(destinationPath));
        }

        source.DataSource = sourcePath;
        source.Mode = SqliteOpenMode.ReadOnly;
        source.Cache = SqliteCacheMode.Private;
        source.Pooling = false;
        var sourceConnectionString = source.ToString();

        // BackupDatabase is synchronous; use dedicated connections off the UI thread.
        var sourceLock = SourceLocks.GetOrAdd(sourcePath, _ => new SemaphoreSlim(1, 1));
        await sourceLock.WaitAsync(ct);
        try { await Task.Run(() => CreateBackupAsync(sourceConnectionString, targetPath, validate, ct), ct); }
        finally { sourceLock.Release(); }
    }

    private static async Task CreateBackupAsync(string sourceConnectionString, string targetPath,
        Func<string, CancellationToken, Task>? validate, CancellationToken ct)
    {
        var temporaryPath = Path.Combine(Path.GetDirectoryName(targetPath)!, $".saldo-backup-{Guid.NewGuid():N}.db");
        try
        {
            ct.ThrowIfCancellationRequested();
            using (var source = new SqliteConnection(sourceConnectionString))
            using (var target = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = temporaryPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false
            }.ToString()))
            {
                source.Open();
                target.Open();
                // SQLite's online backup includes committed WAL data in a consistent snapshot.
                // https://learn.microsoft.com/dotnet/standard/data/sqlite/backup
                source.BackupDatabase(target);
                // Make the backup self-contained even when the source uses WAL mode.
                using var journalMode = target.CreateCommand();
                journalMode.CommandText = "PRAGMA journal_mode=DELETE;";
                journalMode.ExecuteNonQuery();
                using var check = target.CreateCommand();
                check.CommandText = "PRAGMA quick_check;";
                if (!string.Equals(check.ExecuteScalar() as string, "ok", StringComparison.Ordinal))
                    throw new InvalidDataException("The backup failed SQLite integrity validation.");
            }

            ct.ThrowIfCancellationRequested();
            if (validate is not null) await validate(temporaryPath, ct);
            ct.ThrowIfCancellationRequested();
            // Finish the snapshot before replacing any previous backup at the selected path.
            File.Move(temporaryPath, targetPath, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }
}
