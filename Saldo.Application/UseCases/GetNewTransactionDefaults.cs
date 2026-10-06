using Saldo.Application.DTOs;
using Saldo.Application.Interfaces;
using Saldo.Domain.Enums;

namespace Saldo.Application.UseCases;

public sealed class GetNewTransactionDefaults(ITransactionSettingsRepository settings)
{
    public async Task<NewTransactionDefaultsDto> ExecuteAsync(CancellationToken ct = default)
    {
        var current = await settings.GetAsync(ct);
        return new NewTransactionDefaultsDto(
            DateOnly.FromDateTime(DateTime.Today), TransactionType.Expense, current.DefaultPayerId);
    }
}
