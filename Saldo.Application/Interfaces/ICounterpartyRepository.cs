using Saldo.Domain.Entities;

namespace Saldo.Application.Interfaces;

public interface ICounterpartyRepository
{
    Task<Counterparty?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Counterparty>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(Counterparty counterparty, CancellationToken ct = default);
    Task UpdateAsync(Counterparty counterparty, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
