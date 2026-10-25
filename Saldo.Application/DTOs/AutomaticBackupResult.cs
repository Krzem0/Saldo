namespace Saldo.Application.DTOs;

public sealed record AutomaticBackupResult(string Path, DateOnly Date, bool Created, bool RetentionSucceeded);
