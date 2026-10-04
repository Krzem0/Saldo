namespace Saldo.Application.Interfaces;

public interface IDatabaseBackupService
{
    Task CreateAsync(string destinationPath, CancellationToken ct = default);
}
