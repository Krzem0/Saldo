using Saldo.Domain.Entities;

namespace Saldo.Application.Interfaces;

public interface ITransactionSettingsRepository
{
    Task<TransactionSettings> GetAsync(CancellationToken ct = default);
    Task SaveAsync(TransactionSettings settings, CancellationToken ct = default);
}
