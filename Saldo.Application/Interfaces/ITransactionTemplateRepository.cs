using Saldo.Domain.Entities;

namespace Saldo.Application.Interfaces;

public interface ITransactionTemplateRepository
{
    Task<TransactionTemplate?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<TransactionTemplate>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(TransactionTemplate template, CancellationToken ct = default);
    Task UpdateAsync(TransactionTemplate template, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
