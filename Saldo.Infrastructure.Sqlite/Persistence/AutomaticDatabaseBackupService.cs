using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Saldo.Application.DTOs;
using Saldo.Application.Interfaces;
using Saldo.Application.UseCases;
using Saldo.Infrastructure.Sqlite.Repositories;

namespace Saldo.Infrastructure.Sqlite.Persistence;

public sealed class AutomaticDatabaseBackupService : IAutomaticDatabaseBackupService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> DatabaseLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _connectionString;
    private readonly string _directory;
    private readonly string _prefix;
    private readonly SemaphoreSlim _gate;
    private readonly IDatabaseBackupService _backups;
    private readonly TimeProvider _clock;
    private readonly ILogger<AutomaticDatabaseBackupService> _logger;

    public AutomaticDatabaseBackupService(string connectionString, TimeProvider? clock = null,
        ILogger<AutomaticDatabaseBackupService>? logger = null)
    {
        var source = new SqliteConnectionStringBuilder(connectionString);
        var path = Path.GetFullPath(source.DataSource);
        source.DataSource = path;
        source.Mode = SqliteOpenMode.ReadOnly;
        source.Pooling = false;
        _connectionString = source.ToString();
        _directory = Path.Combine(Path.GetDirectoryName(path)!, "AutoBackups");
        _prefix = Path.GetFileName(path) + ".auto-";
        _gate = DatabaseLocks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
        _backups = new SqliteDatabaseBackupService(_connectionString);
        _clock = clock ?? TimeProvider.System;
        _logger = logger ?? NullLogger<AutomaticDatabaseBackupService>.Instance;
    }

    public Task<AutomaticBackupResult> EnsureDailyAsync(CancellationToken ct = default) => RunAsync(null, ct);
    public Task<AutomaticBackupResult> CreateAfterClosingAsync(int year, int month, CancellationToken ct = default) =>
        RunAsync((year, month), ct);

    private Task<AutomaticBackupResult> RunAsync((int Year, int Month)? closedMonth, CancellationToken ct) => Task.Run(async () =>
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (closedMonth is { } month)
            {
                await using var context = new SaldoDbContext(new DbContextOptionsBuilder<SaldoDbContext>()
                    .UseSqlite(_connectionString).Options);
                var result = await new VerifyAccountingPeriod(new AccountingPeriodRepository(context))
                    .ExecuteAsync(month.Year, month.Month, ct);
                if (result.IsFailed || result.Value.Status != AccountingPeriodVerificationStatus.Valid)
                    throw new InvalidDataException("The closed period must pass integrity verification before replacing its daily backup.");
            }

            ct.ThrowIfCancellationRequested();
            var date = DateOnly.FromDateTime(_clock.GetLocalNow().DateTime);
            Directory.CreateDirectory(_directory);
            var target = Path.Combine(_directory, _prefix + date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".db");
            var created = closedMonth.HasValue || !IsValidBackup(target);
            if (created) await _backups.CreateAsync(target, ct);
            var retentionSucceeded = Prune(target, ct);
            _logger.LogInformation("Automatic backup ready at {Path}; created: {Created}; retention completed: {RetentionSucceeded}.", target, created, retentionSucceeded);
            return new AutomaticBackupResult(target, date, created, retentionSucceeded);
        }
        finally { _gate.Release(); }
    }, ct);

    private static bool IsValidBackup(string path)
    {
        if (!File.Exists(path) || new FileInfo(path).Length == 0) return false;
        try
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false
            }.ToString());
            connection.Open();
            using var check = connection.CreateCommand();
            check.CommandText = "PRAGMA quick_check;";
            if (!string.Equals(check.ExecuteScalar() as string, "ok", StringComparison.Ordinal)) return false;
            check.CommandText = "SELECT COUNT(*) FROM sqlite_schema WHERE type = 'table' AND name IN ('Transactions', 'AccountingPeriods');";
            return Convert.ToInt32(check.ExecuteScalar(), CultureInfo.InvariantCulture) == 2;
        }
        catch (SqliteException) { return false; }
    }

    private bool Prune(string currentPath, CancellationToken ct)
    {
        var succeeded = true;
        try
        {
            // Only exact filenames owned by this database participate in retention.
            // Always keep the just-created day, even if the system clock moved back.
            var candidates = Directory.EnumerateFiles(_directory).Select(path => (Path: path, Name: Path.GetFileName(path)))
                .Where(file => file.Name.StartsWith(_prefix, StringComparison.OrdinalIgnoreCase)
                    && file.Name.Length == _prefix.Length + 13 && file.Name.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                .Select(file => (file.Path, Date: DateOnly.TryParseExact(file.Name.AsSpan(_prefix.Length, 10), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date) ? date : (DateOnly?)null))
                .Where(file => file.Date.HasValue && !string.Equals(file.Path, currentPath, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(file => file.Date).Skip(4).ToList();
            foreach (var file in candidates)
            {
                ct.ThrowIfCancellationRequested();
                try { File.Delete(file.Path); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    succeeded = false;
                    _logger.LogWarning(ex, "Could not delete expired automatic backup {Path}.", file.Path);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            succeeded = false;
            _logger.LogWarning(ex, "Could not complete automatic backup retention.");
        }
        return succeeded;
    }
}
