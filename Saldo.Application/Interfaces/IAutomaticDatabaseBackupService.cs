using Saldo.Application.DTOs;

namespace Saldo.Application.Interfaces;

public interface IAutomaticDatabaseBackupService
{
    Task<AutomaticBackupResult> EnsureDailyAsync(CancellationToken ct = default);
    Task<AutomaticBackupResult> CreateAfterClosingAsync(int year, int month, CancellationToken ct = default);
}
