using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Saldo.Application.Interfaces;

namespace Saldo.Infrastructure.Sqlite.Persistence;

public sealed class SqliteDatabaseBackupService(SaldoDbContext context) : IDatabaseBackupService
{
    public Task CreateAsync(string destinationPath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        var source = new SqliteConnectionStringBuilder(context.Database.GetConnectionString());
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
        return Task.Run(() => CreateBackup(sourceConnectionString, targetPath, ct), ct);
    }

    private static void CreateBackup(string sourceConnectionString, string targetPath, CancellationToken ct)
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
            }

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
