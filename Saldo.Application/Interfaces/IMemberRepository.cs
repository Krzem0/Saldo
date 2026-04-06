using Saldo.Domain.Entities;

namespace Saldo.Application.Interfaces;

public interface IMemberRepository
{
    Task<Member?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Member>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(Member member, CancellationToken ct = default);
    Task UpdateAsync(Member member, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
