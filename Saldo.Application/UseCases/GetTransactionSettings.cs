using Saldo.Application.Interfaces;
using Saldo.Domain.Entities;

namespace Saldo.Application.UseCases;

public sealed class GetTransactionSettings(ITransactionSettingsRepository settings)
{
    public Task<TransactionSettings> ExecuteAsync(CancellationToken ct = default) => settings.GetAsync(ct);
}
