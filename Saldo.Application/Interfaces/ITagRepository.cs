using Saldo.Domain.Entities;

namespace Saldo.Application.Interfaces;

public interface ITagRepository
{
    Task<Tag?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Tag>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(Tag tag, CancellationToken ct = default);
    Task UpdateAsync(Tag tag, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
