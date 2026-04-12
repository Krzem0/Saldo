using Saldo.Domain.Entities;

namespace Saldo.Application.Interfaces;

public interface IPartyRepository
{
    Task<Party?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Party>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(Party party, CancellationToken ct = default);
    Task UpdateAsync(Party party, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
