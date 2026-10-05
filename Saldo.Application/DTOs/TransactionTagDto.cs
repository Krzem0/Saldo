namespace Saldo.Application.DTOs;

public sealed record TransactionTagDto(int Id, string Name, string? ColorCode, string? IconKey = null);
