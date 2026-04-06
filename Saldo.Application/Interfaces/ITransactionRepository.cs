using Saldo.Domain.Entities;

namespace Saldo.Application.Interfaces;

public interface ITransactionRepository
{
    Task<Transaction?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Transaction>> GetByMonthAsync(int year, int month, CancellationToken ct = default);
    Task AddAsync(Transaction transaction, CancellationToken ct = default);
    Task UpdateAsync(Transaction transaction, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
