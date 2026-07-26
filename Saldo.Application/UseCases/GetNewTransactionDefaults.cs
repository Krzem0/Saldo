using Saldo.Application.DTOs;
using Saldo.Application.Interfaces;
using Saldo.Domain.Enums;

namespace Saldo.Application.UseCases;

public sealed class GetNewTransactionDefaults
{
    private readonly IPartyRepository _parties;

    public GetNewTransactionDefaults(IPartyRepository parties)
    {
        _parties = parties;
    }

    public async Task<NewTransactionDefaultsDto> ExecuteAsync(CancellationToken ct = default)
    {
        var parties = await _parties.GetAllAsync(ct);
        var defaultPayer = parties.FirstOrDefault(p => string.Equals(p.Name, "Ja", StringComparison.OrdinalIgnoreCase))
            ?? parties.FirstOrDefault(p => string.Equals(p.Name, "Me", StringComparison.OrdinalIgnoreCase))
            ?? parties.FirstOrDefault();

        return new NewTransactionDefaultsDto(
            DateOnly.FromDateTime(DateTime.Today),
            TransactionType.Expense,
            defaultPayer?.Id);
    }
}
